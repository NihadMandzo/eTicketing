using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Payment.Business.Payments.Gateways;
using eTicketing.Payment.Data.Entities;
using eTicketing.Payment.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;

namespace eTicketing.Payment.Business.Payments;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IStripeCustomerRepository _customerRepository;
    private readonly IPaymentGateway _gateway;
    private readonly StripeOptions _stripeOptions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentRepository paymentRepository,
        IStripeCustomerRepository customerRepository,
        IPaymentGateway gateway,
        IOptions<StripeOptions> stripeOptions,
        IUnitOfWork unitOfWork,
        ILogger<PaymentService> logger)
    {
        _paymentRepository = paymentRepository;
        _customerRepository = customerRepository;
        _gateway = gateway;
        _stripeOptions = stripeOptions.Value;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<PaymentIntentResponse>> CreateIntentAsync(CreateIntentRequest request, CancellationToken ct = default)
    {
        var currency = _stripeOptions.Currency;
        var metadata = BuildMetadata(request);

        try
        {
            if (request.IsSubscription)
                return await CreateSubscriptionIntentAsync(request, currency, metadata, ct);

            // The gateway is handed OrderRef as its idempotency key, so a replayed create returns
            // the SAME provider intent -- including its client secret -- rather than a second one.
            // That is why this does not need to short-circuit on an existing row: the provider is
            // already the authority on uniqueness, and the local row is simply brought up to date.
            var intent = await _gateway.CreateIntentAsync(
                new GatewayIntentRequest(
                    request.Amount, currency, request.OrderRef, request.UserId,
                    request.CustomerEmail, request.Description, metadata),
                ct);

            await UpsertPendingAsync(request, currency, intent.IntentId, providerSubscriptionId: null, ct);

            return Result<PaymentIntentResponse>.Success(new PaymentIntentResponse(
                _gateway.Name, _gateway.PublishableKey, intent.IntentId, intent.ClientSecret,
                request.Amount, currency, SubscriptionReference: null));
        }
        catch (PaymentGatewayUnavailableException ex)
        {
            return ProviderUnavailable<PaymentIntentResponse>(ex, request.OrderRef, "kreiranje namjere plaćanja");
        }
    }

    private async Task<Result<PaymentIntentResponse>> CreateSubscriptionIntentAsync(
        CreateIntentRequest request,
        string currency,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SubscriptionProductName))
            return Result<PaymentIntentResponse>.Failure(Error.Validation(
                "payment.subscription_name_required", "Naziv pretplate je obavezan."));

        if (string.IsNullOrWhiteSpace(request.CustomerEmail))
            return Result<PaymentIntentResponse>.Failure(Error.Validation(
                "payment.customer_email_required", "Email kupca je obavezan za pretplatu."));

        var customerReference = await ResolveCustomerAsync(request.UserId, request.CustomerEmail, ct);

        var subscription = await _gateway.CreateSubscriptionAsync(
            new GatewaySubscriptionRequest(
                request.Amount, currency, request.OrderRef, request.UserId,
                customerReference, request.SubscriptionProductName, metadata),
            ct);

        // No ProviderPaymentIntentId on this path: the first invoice's PaymentIntent id is not
        // exposed by the confirmation-secret expansion, and the subscription reference is what every
        // later webhook is keyed by anyway.
        await UpsertPendingAsync(request, currency, providerIntentId: null,
            providerSubscriptionId: subscription.SubscriptionId, ct);

        return Result<PaymentIntentResponse>.Success(new PaymentIntentResponse(
            _gateway.Name, _gateway.PublishableKey, subscription.SubscriptionId, subscription.ClientSecret,
            request.Amount, currency, subscription.SubscriptionId));
    }

    public async Task<Result<PaymentResponse>> CaptureAsync(CapturePaymentRequest request, CancellationToken ct = default)
    {
        var payment = await _paymentRepository.GetByOrderRefAsync(request.OrderRef, ct);
        if (payment is null)
            return Result<PaymentResponse>.Failure(Error.NotFound(
                "payment.not_found", "Plaćanje za ovu narudžbu nije pronađeno."));

        // Idempotent replay: Ticketing retried POST /purchases after a lost response. The money is
        // already captured, so report the original outcome instead of capturing a second time.
        if (payment.Status == PaymentStatus.Succeeded)
            return Result<PaymentResponse>.Success(ToResponse(payment));

        // Binding check against our OWN record, independent of the provider-side metadata check the
        // gateway also performs. A caller who presents someone else's intent id fails here first.
        if (!string.Equals(payment.ProviderPaymentIntentId, request.IntentId, StringComparison.Ordinal))
            return Result<PaymentResponse>.Success(await MarkFailedAsync(payment, "intent_mismatch", ct));

        if (payment.UserId is not null && payment.UserId != request.UserId)
            return Result<PaymentResponse>.Success(await MarkFailedAsync(payment, "intent_user_mismatch", ct));

        if (payment.Amount != request.ExpectedAmount)
            return Result<PaymentResponse>.Success(await MarkFailedAsync(payment, "intent_amount_mismatch", ct));

        try
        {
            var result = await _gateway.CaptureAsync(
                new GatewayCaptureRequest(
                    request.IntentId, request.OrderRef, request.UserId,
                    request.ExpectedAmount, payment.Currency, request.SimulatedLast4),
                ct);

            payment.Status = MapStatus(result.Status);
            payment.FailureCode = result.FailureCode;
            await _unitOfWork.SaveChangesAsync(ct);

            return Result<PaymentResponse>.Success(ToResponse(payment));
        }
        catch (PaymentGatewayUnavailableException ex)
        {
            return ProviderUnavailable<PaymentResponse>(ex, request.OrderRef, "naplata plaćanja");
        }
    }

    public async Task<Result<SubscriptionChargeResponse>> ConfirmSubscriptionAsync(
        ConfirmSubscriptionRequest request, CancellationToken ct = default)
    {
        var payment = await _paymentRepository.GetByOrderRefAsync(request.OrderRef, ct);
        if (payment is null)
            return Result<SubscriptionChargeResponse>.Failure(Error.NotFound(
                "payment.not_found", "Plaćanje za ovu narudžbu nije pronađeno."));

        if (!string.Equals(payment.ProviderSubscriptionId, request.SubscriptionReference, StringComparison.Ordinal))
        {
            var failed = await MarkFailedAsync(payment, "subscription_mismatch", ct);
            return Result<SubscriptionChargeResponse>.Success(ToSubscriptionResponse(failed, request.SubscriptionReference, null));
        }

        try
        {
            var result = await _gateway.ConfirmSubscriptionAsync(
                request.SubscriptionReference, request.SimulatedLast4, ct);

            payment.Status = MapStatus(result.Status);
            payment.FailureCode = result.FailureCode;
            await _unitOfWork.SaveChangesAsync(ct);

            return Result<SubscriptionChargeResponse>.Success(
                ToSubscriptionResponse(ToResponse(payment), request.SubscriptionReference, result));
        }
        catch (PaymentGatewayUnavailableException ex)
        {
            return ProviderUnavailable<SubscriptionChargeResponse>(ex, request.OrderRef, "potvrda pretplate");
        }
    }

    public async Task<Result> CancelIntentAsync(CancelIntentRequest request, CancellationToken ct = default)
    {
        try
        {
            await _gateway.CancelIntentAsync(request.IntentId, ct);
        }
        catch (PaymentGatewayUnavailableException ex)
        {
            // Compensation is best-effort by nature: the caller is already returning a failure to
            // the buyer, and an uncaptured authorization expires on its own at the provider. Say so
            // loudly rather than turning a clean "your reservation expired" into a 503.
            _logger.LogError(ex, "Otkazivanje namjere plaćanja {IntentId} nije uspjelo.", request.IntentId);
        }

        var payment = await _paymentRepository.GetByProviderIntentIdAsync(request.IntentId, ct);
        if (payment is not null && payment.Status == PaymentStatus.Pending)
        {
            payment.Status = PaymentStatus.Cancelled;
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    public async Task<Result> CancelSubscriptionAsync(CancelSubscriptionRequest request, CancellationToken ct = default)
    {
        try
        {
            await _gateway.CancelSubscriptionAsync(request.SubscriptionReference, request.AtPeriodEnd, ct);

            if (request.RefundLastInvoice)
            {
                // Subscription invoices cannot be manual-capture, so unlike the one-time path the
                // money really did move and has to come back.
                var payment = await _paymentRepository.GetLatestBySubscriptionIdAsync(request.SubscriptionReference, ct);
                if (payment?.ProviderPaymentIntentId is not null)
                {
                    await _gateway.RefundAsync(payment.ProviderPaymentIntentId, ct);
                    payment.Status = PaymentStatus.Refunded;
                    await _unitOfWork.SaveChangesAsync(ct);
                }
            }
        }
        catch (PaymentGatewayUnavailableException ex)
        {
            return ProviderUnavailable(ex, request.SubscriptionReference, "otkazivanje pretplate");
        }

        return Result.Success();
    }

    /// <summary>
    /// Returns the buyer's provider customer, creating and persisting it on first use so a buyer
    /// with several parking subscriptions has one customer and one saved card rather than one per
    /// subscription.
    /// </summary>
    private async Task<string> ResolveCustomerAsync(Guid userId, string email, CancellationToken ct)
    {
        var existing = await _customerRepository.GetByUserIdAsync(userId, ct);
        if (existing is not null)
            return existing.ProviderCustomerId;

        var reference = await _gateway.CreateCustomerAsync(userId, email, ct);

        await _customerRepository.AddAsync(
            new StripeCustomer
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProviderCustomerId = reference,
                Email = email,
            },
            ct);

        try
        {
            // A deliberate second SaveChanges in this request, against the usual one-save-per-service-
            // method rule: the provider customer already exists by this line, so deferring the write
            // to the end would orphan it at the provider if anything below fails.
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent first subscription won the unique index on UserId. Its row is
            // authoritative -- both requests are looking at the same provider customer anyway,
            // because CreateCustomerAsync is idempotent on UserId.
            var winner = await _customerRepository.GetByUserIdAsync(userId, ct);
            if (winner is not null)
                return winner.ProviderCustomerId;

            throw;
        }

        return reference;
    }

    private async Task UpsertPendingAsync(
        CreateIntentRequest request,
        string currency,
        string? providerIntentId,
        string? providerSubscriptionId,
        CancellationToken ct)
    {
        var existing = await _paymentRepository.GetByOrderRefAsync(request.OrderRef, ct);
        if (existing is not null)
        {
            // A replayed create. The provider returned the same object (OrderRef is its idempotency
            // key), so this just brings the local row up to date.
            existing.ProviderPaymentIntentId = providerIntentId ?? existing.ProviderPaymentIntentId;
            existing.ProviderSubscriptionId = providerSubscriptionId ?? existing.ProviderSubscriptionId;
            existing.Amount = request.Amount;
            existing.Currency = currency;
            existing.UserId = request.UserId;
            await _unitOfWork.SaveChangesAsync(ct);
            return;
        }

        await _paymentRepository.AddAsync(
            new PaymentEntity
            {
                Id = Guid.NewGuid(),
                Amount = request.Amount,
                Status = PaymentStatus.Pending,
                OrderRef = request.OrderRef,
                Currency = currency,
                Provider = _gateway.Name == PaymentProviderNames.Stripe ? PaymentProvider.Stripe : PaymentProvider.Mock,
                UserId = request.UserId,
                ProviderPaymentIntentId = providerIntentId,
                ProviderSubscriptionId = providerSubscriptionId,
            },
            ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two concurrent intent requests for the same order raced past the lookup above and the
            // unique OrderRef index rejected the loser. The winner's row describes the same provider
            // intent, so losing is not a failure.
            _logger.LogInformation(
                "Istovremeni zahtjev je već kreirao zapis plaćanja za OrderRef {OrderRef}.", request.OrderRef);
        }
    }

    private async Task<PaymentResponse> MarkFailedAsync(PaymentEntity payment, string failureCode, CancellationToken ct)
    {
        _logger.LogWarning(
            "Plaćanje za OrderRef {OrderRef} odbijeno prije poziva provajderu: {FailureCode}.",
            payment.OrderRef, failureCode);

        payment.Status = PaymentStatus.Failed;
        payment.FailureCode = failureCode;
        await _unitOfWork.SaveChangesAsync(ct);

        return ToResponse(payment);
    }

    private static IReadOnlyDictionary<string, string> BuildMetadata(CreateIntentRequest request) =>
        new Dictionary<string, string>
        {
            [GatewayMetadataKeys.OrderRef] = request.OrderRef,
            [GatewayMetadataKeys.UserId] = request.UserId.ToString(),
            [GatewayMetadataKeys.HoldRef] = request.HoldRef,
            [GatewayMetadataKeys.SectorId] = request.SectorId.ToString(),
        };

    /// <summary>
    /// Only a broken provider lands here. It becomes ErrorType.Failure, which ResultExtensions maps
    /// to 503 -- the status Ticketing's HttpPaymentClient already reads as "unavailable" and answers
    /// by releasing the hold. A decline must never take this path.
    /// </summary>
    private Result<T> ProviderUnavailable<T>(Exception ex, string reference, string operation)
    {
        _logger.LogError(ex, "Provajder plaćanja nije dostupan ({Operation}), referenca {Reference}.", operation, reference);

        return Result<T>.Failure(Error.Failure(
            "payment.provider_unavailable", "Plaćanje trenutno nije dostupno. Pokušajte kasnije."));
    }

    private Result ProviderUnavailable(Exception ex, string reference, string operation)
    {
        _logger.LogError(ex, "Provajder plaćanja nije dostupan ({Operation}), referenca {Reference}.", operation, reference);

        return Result.Failure(Error.Failure(
            "payment.provider_unavailable", "Plaćanje trenutno nije dostupno. Pokušajte kasnije."));
    }

    private static PaymentStatus MapStatus(GatewayPaymentStatus status) => status switch
    {
        GatewayPaymentStatus.Succeeded => PaymentStatus.Succeeded,
        GatewayPaymentStatus.Cancelled => PaymentStatus.Cancelled,
        GatewayPaymentStatus.Failed => PaymentStatus.Failed,
        // RequiresPaymentMethod / RequiresAction / RequiresCapture all mean the buyer has not
        // finished yet, which is exactly what Pending records.
        _ => PaymentStatus.Pending,
    };

    private static PaymentResponse ToResponse(PaymentEntity payment) =>
        new(payment.Id, payment.Amount, payment.Status, payment.OrderRef,
            payment.Currency, payment.FailureCode, payment.CreatedAt);

    private static SubscriptionChargeResponse ToSubscriptionResponse(
        PaymentResponse payment, string subscriptionReference, GatewaySubscriptionResult? result) =>
        new(payment.Id, payment.Amount, payment.Status, payment.OrderRef, payment.Currency,
            payment.FailureCode, subscriptionReference,
            // A mismatch is reported before the provider is ever consulted, so there is no period to
            // report; PurchaseService discards it anyway once it sees Status = Failed.
            result?.CurrentPeriodStart ?? DateOnly.FromDateTime(payment.CreatedAt),
            result?.CurrentPeriodEnd ?? DateOnly.FromDateTime(payment.CreatedAt));
}
