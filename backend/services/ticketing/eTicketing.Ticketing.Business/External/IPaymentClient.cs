namespace eTicketing.Ticketing.Business.External;

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
