using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace eTicketing.Payment.Business.Payments.Gateways;

/// <summary>
/// The real gateway, against Stripe test mode.
///
/// Two things here are load-bearing and should not be "simplified" away:
///
/// 1. One-time intents are created with CaptureMethod = "manual". Confirming client-side then
///    yields an authorization ("requires_capture"), not a charge, and POST /purchases captures it
///    only after re-checking that the Redis hold is still alive. The hold TTL is five minutes
///    (SectorService.HoldTtl); without manual capture, a buyer who confirms just as their hold
///    lapses is charged for a ticket that is never minted, and the only remedy is a refund. With
///    it, the remedy is CancelIntentAsync and no money ever moves.
///
/// 2. Nothing the client sends is trusted. CaptureAsync re-reads the intent from Stripe and checks
///    the amount, the currency and the order_ref/user_id metadata this service wrote at creation,
///    so a stolen or swapped payment-intent id fails instead of buying someone a ticket.
///
/// A decline is a normal answer and comes back as a GatewayCaptureResult with Status = Failed. Only
/// Stripe itself being broken (5xx, connection error, rate limit) throws
/// PaymentGatewayUnavailableException, which is what ultimately produces a 503 and releases the hold.
/// </summary>
public class StripePaymentGateway : IPaymentGateway
{
    private readonly StripeOptions _options;
    private readonly ILogger<StripePaymentGateway> _logger;
    private readonly PaymentIntentService _intents;
    private readonly SubscriptionService _subscriptions;
    private readonly CustomerService _customers;
    private readonly ProductService _products;
    private readonly RefundService _refunds;

    public StripePaymentGateway(
        IStripeClient client,
        IOptions<StripeOptions> options,
        ILogger<StripePaymentGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
        _intents = new PaymentIntentService(client);
        _subscriptions = new SubscriptionService(client);
        _customers = new CustomerService(client);
        _products = new ProductService(client);
        _refunds = new RefundService(client);
    }

    public string Name => PaymentProviderNames.Stripe;

    public string? PublishableKey =>
        string.IsNullOrWhiteSpace(_options.PublishableKey) ? null : _options.PublishableKey;

    public async Task<GatewayIntentResult> CreateIntentAsync(GatewayIntentRequest request, CancellationToken ct = default)
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = MoneyConverter.ToMinorUnits(request.Amount),
            Currency = request.Currency,
            CaptureMethod = "manual",
            Description = request.Description,
            ReceiptEmail = request.CustomerEmail,
            Metadata = new Dictionary<string, string>(request.Metadata),
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                // No redirect-based methods. A redirect would send the buyer off-site and back,
                // which routinely outlasts the five-minute hold; keeping only in-page methods means
                // 3-D Secure resolves in Stripe's own modal and the buyer never leaves checkout.
                AllowRedirects = "never",
            },
        };

        // OrderRef is already unique per order (it is the Payments table's unique index), so reusing
        // it as the idempotency key makes Ticketing's Polly retry safe at the provider too: a
        // replayed create returns the original intent instead of a second one.
        var intent = await CallAsync(
            () => _intents.CreateAsync(options, Idempotent(request.OrderRef), ct),
            "kreiranje namjere plaćanja");

        return new GatewayIntentResult(
            intent.Id,
            intent.ClientSecret,
            MapIntentStatus(intent.Status),
            intent.Amount,
            intent.Currency);
    }

    public async Task<GatewayCaptureResult> CaptureAsync(GatewayCaptureRequest request, CancellationToken ct = default)
    {
        var intent = await CallAsync(
            () => _intents.GetAsync(request.IntentId, null, null, ct),
            "očitavanje namjere plaćanja");

        var expectedMinor = MoneyConverter.ToMinorUnits(request.ExpectedAmount);

        // Everything below is the "never trust the client" check. Each mismatch is a Failed result
        // rather than an exception: from the buyer's point of view it is a payment that did not go
        // through, and PurchaseService already knows how to release the hold and answer 400 for that.
        var mismatch = VerifyIntent(intent, request, expectedMinor);
        if (mismatch is not null)
        {
            _logger.LogWarning(
                "Odbijena naplata za OrderRef {OrderRef}: {Reason} (intent {IntentId}, status {Status}).",
                request.OrderRef, mismatch, intent.Id, intent.Status);

            return new GatewayCaptureResult(GatewayPaymentStatus.Failed, mismatch, intent.Amount, intent.Currency);
        }

        // Already captured: happens on a retried POST /purchases after a lost response, and must
        // report success rather than capturing a second time.
        if (intent.Status == "succeeded")
            return new GatewayCaptureResult(GatewayPaymentStatus.Succeeded, null, intent.Amount, intent.Currency);

        if (intent.Status != "requires_capture")
        {
            var failureCode = intent.LastPaymentError?.DeclineCode
                              ?? intent.LastPaymentError?.Code
                              ?? intent.Status;

            return new GatewayCaptureResult(GatewayPaymentStatus.Failed, failureCode, intent.Amount, intent.Currency);
        }

        var captured = await CallAsync(
            () => _intents.CaptureAsync(
                request.IntentId,
                new PaymentIntentCaptureOptions(),
                Idempotent($"{request.OrderRef}-capture"),
                ct),
            "naplata namjere plaćanja");

        return new GatewayCaptureResult(
            MapIntentStatus(captured.Status),
            captured.Status == "succeeded" ? null : captured.LastPaymentError?.DeclineCode ?? captured.Status,
            captured.Amount,
            captured.Currency);
    }

    /// <summary>Returns a failure code when the intent does not match what the caller claims, or
    /// null when everything lines up.</summary>
    private static string? VerifyIntent(PaymentIntent intent, GatewayCaptureRequest request, long expectedMinor)
    {
        if (!MetadataMatches(intent.Metadata, GatewayMetadataKeys.OrderRef, request.OrderRef))
            return "intent_order_mismatch";

        if (!MetadataMatches(intent.Metadata, GatewayMetadataKeys.UserId, request.UserId.ToString()))
            return "intent_user_mismatch";

        if (intent.Amount != expectedMinor)
            return "intent_amount_mismatch";

        if (!string.Equals(intent.Currency, request.ExpectedCurrency, StringComparison.OrdinalIgnoreCase))
            return "intent_currency_mismatch";

        return null;
    }

    private static bool MetadataMatches(IDictionary<string, string>? metadata, string key, string expected) =>
        metadata is not null
        && metadata.TryGetValue(key, out var actual)
        && string.Equals(actual, expected, StringComparison.Ordinal);

    public async Task CancelIntentAsync(string intentId, CancellationToken ct = default)
    {
        await CallAsync(
            () => _intents.CancelAsync(intentId, new PaymentIntentCancelOptions(), null, ct),
            "otkazivanje namjere plaćanja");
    }

    public async Task<GatewaySubscriptionResult> CreateSubscriptionAsync(GatewaySubscriptionRequest request, CancellationToken ct = default)
    {
        // Stripe's subscription price_data takes a Product id and, unlike Checkout line items, has
        // no inline product_data, so the Product is minted here. One ad-hoc Product per subscription
        // keeps organizer price edits from needing any Stripe-side sync at all; the cost is
        // dashboard clutter, which is the right trade at this platform's volume.
        var product = await CallAsync(
            () => _products.CreateAsync(
                new ProductCreateOptions
                {
                    Name = request.ProductName,
                    Metadata = new Dictionary<string, string>(request.Metadata),
                },
                Idempotent($"{request.OrderRef}-product"),
                ct),
            "kreiranje Stripe proizvoda");

        var options = new SubscriptionCreateOptions
        {
            Customer = request.CustomerReference,
            Items =
            [
                new SubscriptionItemOptions
                {
                    PriceData = new SubscriptionItemPriceDataOptions
                    {
                        Currency = request.Currency,
                        Product = product.Id,
                        UnitAmount = MoneyConverter.ToMinorUnits(request.AmountPerPeriod),
                        Recurring = new SubscriptionItemPriceDataRecurringOptions { Interval = "month" },
                    },
                },
            ],
            // Period one becomes a real invoice the client confirms with the same Elements instance
            // a one-time purchase uses, which keeps period one structurally identical to every
            // renewal that follows it.
            PaymentBehavior = "default_incomplete",
            PaymentSettings = new SubscriptionPaymentSettingsOptions
            {
                // Without this there is no card on file to charge next month, and every renewal
                // fails immediately.
                SaveDefaultPaymentMethod = "on_subscription",
            },
            Metadata = new Dictionary<string, string>(request.Metadata),
            Expand = ["latest_invoice.confirmation_secret"],
        };

        var subscription = await CallAsync(
            () => _subscriptions.CreateAsync(options, Idempotent(request.OrderRef), ct),
            "kreiranje pretplate");

        return ToSubscriptionResult(subscription);
    }

    public async Task<GatewaySubscriptionResult> ConfirmSubscriptionAsync(
        string subscriptionId, string? simulatedLast4, CancellationToken ct = default)
    {
        // simulatedLast4 is the mock gateway's decline switch and is meaningless here.
        var subscription = await CallAsync(
            () => _subscriptions.GetAsync(
                subscriptionId,
                new SubscriptionGetOptions { Expand = ["latest_invoice"] },
                null,
                ct),
            "očitavanje pretplate");

        return ToSubscriptionResult(subscription);
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, bool atPeriodEnd, CancellationToken ct = default)
    {
        if (atPeriodEnd)
        {
            // The buyer keeps the period they already paid for; the space is only freed when Stripe
            // fires customer.subscription.deleted at period end.
            await CallAsync(
                () => _subscriptions.UpdateAsync(
                    subscriptionId,
                    new SubscriptionUpdateOptions { CancelAtPeriodEnd = true },
                    null,
                    ct),
                "otkazivanje pretplate na kraju perioda");

            return;
        }

        await CallAsync(
            () => _subscriptions.CancelAsync(subscriptionId, new SubscriptionCancelOptions(), null, ct),
            "trenutno otkazivanje pretplate");
    }

    public async Task RefundAsync(string paymentIntentId, CancellationToken ct = default)
    {
        await CallAsync(
            () => _refunds.CreateAsync(
                new RefundCreateOptions { PaymentIntent = paymentIntentId },
                Idempotent($"{paymentIntentId}-refund"),
                ct),
            "povrat sredstava");
    }

    public async Task<string> CreateCustomerAsync(Guid userId, string email, CancellationToken ct = default)
    {
        // The UserId -> cus_... mapping is persisted by PaymentService, which calls this only when it
        // has no stored customer for the buyer yet. The idempotency key is keyed on UserId as well,
        // so two concurrent first-time subscriptions still yield a single Stripe customer.
        var customer = await CallAsync(
            () => _customers.CreateAsync(
                new CustomerCreateOptions
                {
                    Email = email,
                    Metadata = new Dictionary<string, string>
                    {
                        [GatewayMetadataKeys.UserId] = userId.ToString(),
                    },
                },
                Idempotent($"customer-{userId}"),
                ct),
            "kreiranje Stripe kupca");

        return customer.Id;
    }

    private static GatewaySubscriptionResult ToSubscriptionResult(Subscription subscription)
    {
        var item = subscription.Items?.Data?.FirstOrDefault();

        // current_period_start/end moved off the Subscription and onto its items in recent Stripe
        // API versions, so they are read from the item. CurrentPeriodEnd is exclusive on Stripe's
        // side (it is the next billing instant), while Ticket.ValidTo is an inclusive last day, so
        // a day comes off here rather than in three places downstream.
        var start = item is not null
            ? DateOnly.FromDateTime(item.CurrentPeriodStart)
            : DateOnly.FromDateTime(DateTime.UtcNow);
        var end = item is not null
            ? DateOnly.FromDateTime(item.CurrentPeriodEnd).AddDays(-1)
            : start.AddMonths(1).AddDays(-1);

        return new GatewaySubscriptionResult(
            subscription.Id,
            subscription.LatestInvoice?.ConfirmationSecret?.ClientSecret,
            null,
            MapSubscriptionStatus(subscription.Status),
            subscription.Status is "active" or "trialing" ? null : subscription.Status,
            start,
            end);
    }

    private static GatewayPaymentStatus MapIntentStatus(string status) => status switch
    {
        "succeeded" => GatewayPaymentStatus.Succeeded,
        "requires_capture" => GatewayPaymentStatus.RequiresCapture,
        "requires_action" or "processing" => GatewayPaymentStatus.RequiresAction,
        "requires_payment_method" or "requires_confirmation" => GatewayPaymentStatus.RequiresPaymentMethod,
        "canceled" => GatewayPaymentStatus.Cancelled,
        _ => GatewayPaymentStatus.Failed,
    };

    private static GatewayPaymentStatus MapSubscriptionStatus(string status) => status switch
    {
        "active" or "trialing" => GatewayPaymentStatus.Succeeded,
        "incomplete" => GatewayPaymentStatus.RequiresPaymentMethod,
        "canceled" or "incomplete_expired" => GatewayPaymentStatus.Cancelled,
        _ => GatewayPaymentStatus.Failed,
    };

    private static RequestOptions Idempotent(string key) => new() { IdempotencyKey = key };

    /// <summary>
    /// Splits "Stripe answered, and the answer was no" from "Stripe is broken". Only the latter
    /// becomes PaymentGatewayUnavailableException, because only that justifies a 503 and telling the
    /// buyer to try again later; a decline must stay a normal, reported outcome.
    /// </summary>
    private async Task<T> CallAsync<T>(Func<Task<T>> call, string operation)
    {
        try
        {
            return await call();
        }
        catch (StripeException ex) when (IsTransient(ex))
        {
            _logger.LogError(ex, "Stripe nije dostupan tokom operacije: {Operation}.", operation);
            throw new PaymentGatewayUnavailableException($"Stripe nije dostupan ({operation}).", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Mrežna greška prema Stripe-u tokom operacije: {Operation}.", operation);
            throw new PaymentGatewayUnavailableException($"Stripe nije dostupan ({operation}).", ex);
        }
    }

    private static bool IsTransient(StripeException ex) =>
        (int)ex.HttpStatusCode >= 500
        || ex.HttpStatusCode == System.Net.HttpStatusCode.TooManyRequests
        || ex.StripeError?.Type is "api_connection_error" or "api_error";
}
