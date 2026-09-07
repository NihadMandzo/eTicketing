namespace eTicketing.Ticketing.Business.External;

/// <summary>
/// Mirrors eTicketing.Payment.Data.Entities.PaymentStatus member-for-member, in order. Both sides
/// are serialized as the integer ordinal (System.Text.Json's default, no converter on either side),
/// so these two separately-declared enums are really one wire format living in two files: APPEND
/// ONLY, never reorder, never remove. PaymentEnumParityTests fails the build if they drift.
/// </summary>
public enum PaymentChargeStatus
{
    Succeeded,
    Failed,
    Pending,
    Refunded,
    Cancelled,
}

/// <summary>Mirrors eTicketing.Payment.Business.Payments.PaymentResponse's JSON shape — duplicated
/// rather than shared across the service boundary, same convention as CatalogProductResponse.</summary>
public record PaymentChargeResponse(
    Guid Id,
    decimal Amount,
    PaymentChargeStatus Status,
    string OrderRef,
    string Currency,
    string? FailureCode);

/// <summary>Mirrors eTicketing.Payment's SubscriptionChargeResponse. Carries the billing period the
/// provider settled on, which is what the Subscription row is stamped with -- so our records and the
/// buyer's card statement can never disagree about what was paid for.</summary>
public record SubscriptionChargeResponse(
    Guid Id,
    decimal Amount,
    PaymentChargeStatus Status,
    string OrderRef,
    string Currency,
    string? FailureCode,
    string SubscriptionReference,
    DateOnly CurrentPeriodStart,
    DateOnly CurrentPeriodEnd);

/// <summary>Everything the client needs to drive the payment: which provider is configured (so it
/// renders the Stripe Elements form or the mock card form), the publishable key when there is one,
/// and the client secret to confirm against.</summary>
public record PaymentIntentCreatedResponse(
    string Provider,
    string? PublishableKey,
    string IntentId,
    string? ClientSecret,
    decimal Amount,
    string Currency,
    string? SubscriptionReference);

/// <summary>HTTP client interface to eTicketing.Payment (internal-only; only the Stripe webhook is
/// routed through the Gateway) — called synchronously from PurchaseService on the purchase-critical
/// path. Interface lives in .Business per .claude/rules/10-backend.md; the concrete HttpClient-backed
/// implementation (registered with the project's flagship circuit breaker) lives in
/// .Api/Infrastructure.</summary>
public interface IPaymentClient
{
    /// <summary>Creates the provider-side intent the buyer confirms in their browser. For a
    /// RecurringReservation sector this creates a real subscription instead, whose first invoice is
    /// confirmed exactly the same way.</summary>
    Task<PaymentIntentCreatedResponse> CreateIntentAsync(
        decimal amount,
        string orderRef,
        Guid userId,
        string userEmail,
        string description,
        string holdRef,
        Guid sectorId,
        bool isSubscription,
        string? subscriptionProductName,
        CancellationToken ct = default);

    /// <summary>Captures a previously authorized intent, after Payment re-reads it from the provider
    /// and verifies amount, currency and metadata. One-time intents are created with manual capture,
    /// so nothing has actually been charged until this succeeds.</summary>
    Task<PaymentChargeResponse> CapturePaymentAsync(
        string intentId,
        string orderRef,
        Guid userId,
        decimal expectedAmount,
        string? simulatedLast4,
        CancellationToken ct = default);

    /// <summary>Confirms that the subscription's first invoice really was paid. The subscription
    /// counterpart of CapturePaymentAsync.</summary>
    Task<SubscriptionChargeResponse> ConfirmSubscriptionAsync(
        string subscriptionReference,
        string orderRef,
        Guid userId,
        decimal expectedAmount,
        string? simulatedLast4,
        CancellationToken ct = default);

    /// <summary>Voids an uncaptured authorization. Best-effort compensation when the hold expired
    /// after the buyer confirmed: no money moved, so this is a cancel and not a refund.</summary>
    Task CancelIntentAsync(string intentId, CancellationToken ct = default);

    /// <param name="atPeriodEnd">True for a buyer-initiated cancellation — they keep the period they
    /// already paid for. False only for compensation, where the subscription must die now and its
    /// first invoice be refunded.</param>
    Task CancelSubscriptionAsync(string subscriptionReference, bool atPeriodEnd, bool refundLastInvoice, CancellationToken ct = default);
}

/// <summary>Thrown by the concrete HttpPaymentClient whenever the resilience pipeline gives up
/// (circuit open, timeout, or a genuine transport failure), and also when Payment itself answers 503
/// because the payment provider is down — a plain, framework-agnostic exception so PurchaseService
/// (in .Business) never needs to reference Polly's types directly; those stay confined to
/// .Api/Infrastructure per .claude/rules/10-backend.md's layering.</summary>
public class PaymentUnavailableException : Exception
{
    public PaymentUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
