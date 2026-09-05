using eTicketing.Contracts.Results;

namespace eTicketing.Payment.Business.Payments;

/// <summary>
/// Orchestrates and persists payments; the provider call itself lives behind IPaymentGateway, which
/// is what lets every test here run without a network.
///
/// The distinction that governs every method: a decline is a normal, expected business outcome and
/// comes back as a SUCCESSFUL Result whose payload carries Status = Failed. A Result failure means
/// something genuinely went wrong -- the provider is down, or the request refers to an order that
/// does not exist. eTicketing.Ticketing's PurchaseService depends on exactly this split: a declined
/// card releases the hold and answers 400, an unavailable provider releases the hold and answers 503.
/// </summary>
public interface IPaymentService
{
    /// <summary>Creates (or, on a replay, returns) the provider intent the buyer confirms in their
    /// browser. Records the payment as Pending; nothing is charged yet.</summary>
    Task<Result<PaymentIntentResponse>> CreateIntentAsync(CreateIntentRequest request, CancellationToken ct = default);

    /// <summary>Captures a previously authorized one-time intent, after the gateway re-reads it from
    /// the provider and verifies amount, currency and metadata.</summary>
    Task<Result<PaymentResponse>> CaptureAsync(CapturePaymentRequest request, CancellationToken ct = default);

    /// <summary>The subscription counterpart of <see cref="CaptureAsync"/>: verifies the first
    /// invoice really was paid, and reports the billing period the provider settled on.</summary>
    Task<Result<SubscriptionChargeResponse>> ConfirmSubscriptionAsync(ConfirmSubscriptionRequest request, CancellationToken ct = default);

    /// <summary>Voids an uncaptured authorization. Compensation for a hold that expired after the
    /// buyer confirmed -- no money moved, so this is a cancel, not a refund.</summary>
    Task<Result> CancelIntentAsync(CancelIntentRequest request, CancellationToken ct = default);

    Task<Result> CancelSubscriptionAsync(CancelSubscriptionRequest request, CancellationToken ct = default);
}
