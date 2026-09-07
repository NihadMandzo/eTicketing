using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Payment.Business.Payments.Gateways;
using eTicketing.Payment.Data.Entities;
using eTicketing.Payment.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;

namespace eTicketing.Payment.Business.Payments.Webhooks;

/// <summary>
/// Turns Stripe's asynchronous notifications into the events eTicketing.Ticketing acts on.
///
/// This is the half of the subscription feature that cannot be synchronous: a monthly renewal starts
/// at Stripe, weeks after anyone was on the site, so the only way the platform learns a parking space
/// was paid for again is a webhook. Every delivery is verified by signature over the raw body,
/// recorded by Stripe's own event id so a redelivery cannot mint a second ticket for a month that was
/// paid for once, and answered fast -- the actual work happens in Ticketing, off the reply path.
/// </summary>
public class StripeWebhookService : IStripeWebhookService
{
    private readonly IStripeSignatureVerifier _verifier;
    private readonly IStripeEventRepository _eventRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly StripeOptions _options;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StripeWebhookService> _logger;

    public StripeWebhookService(
        IStripeSignatureVerifier verifier,
        IStripeEventRepository eventRepository,
        IPaymentRepository paymentRepository,
        IEventPublisher eventPublisher,
        IOptions<StripeOptions> options,
        IUnitOfWork unitOfWork,
        ILogger<StripeWebhookService> logger)
    {
        _verifier = verifier;
        _eventRepository = eventRepository;
        _paymentRepository = paymentRepository;
        _eventPublisher = eventPublisher;
        _options = options.Value;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(string rawBody, string? signatureHeader, CancellationToken ct = default)
    {
        var stripeEvent = _verifier.Verify(rawBody, signatureHeader, _options.WebhookSecret);
        if (stripeEvent is null)
        {
            // The only path that must NOT be a 2xx. This endpoint is the one publicly routed part of
            // the payment service, so an unsigned or wrongly-signed body is the thing being guarded
            // against, not a delivery to retry.
            _logger.LogWarning("Odbijen webhook bez ispravnog Stripe potpisa.");
            return Result.Failure(Error.Unauthorized(
                "payment.invalid_signature", "Potpis zahtjeva nije ispravan."));
        }

        if (await _eventRepository.ExistsAsync(stripeEvent.Id, ct))
        {
            _logger.LogInformation("Stripe događaj {EventId} je već obrađen -- preskačem.", stripeEvent.Id);
            return Result.Success();
        }

        var handled = await DispatchAsync(stripeEvent, ct);

        if (!handled)
        {
            // Deliberately a success. Stripe sends whatever the account is subscribed to, and an
            // event we have no opinion about is not a failure -- answering non-2xx would make it
            // redeliver on a backoff schedule indefinitely.
            _logger.LogDebug("Stripe događaj tipa {Type} se ne obrađuje.", stripeEvent.Type);
            return Result.Success();
        }

        return await RecordProcessedAsync(stripeEvent, ct);
    }

    /// <summary>
    /// Marks the delivery handled. The insert is what makes redelivery safe, and a duplicate-key
    /// violation here means a concurrent delivery of the SAME event won the race -- which is exactly
    /// the outcome the table exists to produce, so it is a success, not an error.
    /// </summary>
    private async Task<Result> RecordProcessedAsync(Event stripeEvent, CancellationToken ct)
    {
        await _eventRepository.AddAsync(
            new StripeEvent { Id = stripeEvent.Id, Type = stripeEvent.Type }, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _logger.LogInformation(
                "Stripe događaj {EventId} je istovremeno obrađen u drugom zahtjevu.", stripeEvent.Id);
        }

        return Result.Success();
    }

    /// <returns>True when the event was one we act on, false for anything else.</returns>
    private async Task<bool> DispatchAsync(Event stripeEvent, CancellationToken ct)
    {
        switch (stripeEvent.Type)
        {
            case "invoice.paid":
                return await HandleInvoicePaidAsync(stripeEvent, ct);

            case "invoice.payment_failed":
                return await HandleInvoiceFailedAsync(stripeEvent, ct);

            case "customer.subscription.deleted":
                return await HandleSubscriptionDeletedAsync(stripeEvent, ct);

            default:
                return false;
        }
    }

    private async Task<bool> HandleInvoicePaidAsync(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not Invoice invoice)
            return false;

        var subscriptionId = SubscriptionIdOf(invoice);
        if (subscriptionId is null)
            return false;

        // The decisive check. "subscription_create" is period one, which POST /purchases already
        // minted synchronously so the buyer left checkout holding a ticket; acting on it here would
        // race that request and mint a duplicate. Only a genuine renewal cycle mints from a webhook.
        if (invoice.BillingReason != "subscription_cycle")
        {
            _logger.LogInformation(
                "Faktura za pretplatu {SubscriptionId} preskočena -- razlog naplate je {BillingReason}.",
                subscriptionId, invoice.BillingReason);
            return false;
        }

        var amount = MoneyConverter.ToMajorUnits(invoice.AmountPaid);
        var periodStart = DateOnly.FromDateTime(invoice.PeriodStart);
        // Stripe's period end is the next billing instant; Ticket.ValidTo is an inclusive last day.
        var periodEnd = DateOnly.FromDateTime(invoice.PeriodEnd).AddDays(-1);

        await RecordRenewalPaymentAsync(subscriptionId, amount, invoice.Currency, ct);

        await _eventPublisher.PublishAsync(
            EventNames.SubscriptionRenewed,
            new SubscriptionRenewed(subscriptionId, amount, invoice.Currency, periodStart, periodEnd, DateTime.UtcNow),
            ct);

        return true;
    }

    private async Task<bool> HandleInvoiceFailedAsync(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not Invoice invoice)
            return false;

        var subscriptionId = SubscriptionIdOf(invoice);
        if (subscriptionId is null)
            return false;

        // No release here: Stripe is still working through its own dunning retries, and the buyer
        // keeps the space while it does. Only customer.subscription.deleted frees it.
        await _eventPublisher.PublishAsync(
            EventNames.SubscriptionPaymentFailed,
            new SubscriptionPaymentFailed(subscriptionId, "renewal_payment_failed", DateTime.UtcNow),
            ct);

        return true;
    }

    private async Task<bool> HandleSubscriptionDeletedAsync(Event stripeEvent, CancellationToken ct)
    {
        if (stripeEvent.Data.Object is not Subscription subscription)
            return false;

        await _eventPublisher.PublishAsync(
            EventNames.SubscriptionCancelled,
            new SubscriptionCancelled(subscription.Id, DateTime.UtcNow),
            ct);

        return true;
    }

    /// <summary>
    /// Books the renewal as its own Payment row. Each billing period is a separate charge, so they
    /// cannot share the first period's row -- and OrderRef stays unique because the provider's
    /// invoice id is what distinguishes them.
    /// </summary>
    private async Task RecordRenewalPaymentAsync(string subscriptionId, decimal amount, string currency, CancellationToken ct)
    {
        var first = await _paymentRepository.GetLatestBySubscriptionIdAsync(subscriptionId, ct);

        await _paymentRepository.AddAsync(
            new PaymentEntity
            {
                Id = Guid.NewGuid(),
                Amount = amount,
                Status = PaymentStatus.Succeeded,
                OrderRef = $"{subscriptionId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                Currency = currency,
                Provider = PaymentProvider.Stripe,
                UserId = first?.UserId,
                ProviderSubscriptionId = subscriptionId,
            },
            ct);
    }

    /// <summary>
    /// An invoice's subscription moved under invoice.parent.subscription_details in recent Stripe API
    /// versions; there is no Invoice.SubscriptionId any more.
    /// </summary>
    private static string? SubscriptionIdOf(Invoice invoice) =>
        invoice.Parent?.SubscriptionDetails?.SubscriptionId;
}
