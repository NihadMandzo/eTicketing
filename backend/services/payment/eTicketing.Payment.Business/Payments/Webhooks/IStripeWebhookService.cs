using eTicketing.Contracts.Results;

namespace eTicketing.Payment.Business.Payments.Webhooks;

public interface IStripeWebhookService
{
    /// <summary>
    /// Verifies, de-duplicates and dispatches one webhook delivery.
    ///
    /// The Result maps onto what the provider is told, and that mapping is load-bearing: a bad
    /// signature must be a 4xx (Error.Unauthorized), but an event type we simply do not care about
    /// must still be a success, because anything non-2xx makes Stripe redeliver it on a backoff
    /// schedule forever.
    /// </summary>
    Task<Result> HandleAsync(string rawBody, string? signatureHeader, CancellationToken ct = default);
}
