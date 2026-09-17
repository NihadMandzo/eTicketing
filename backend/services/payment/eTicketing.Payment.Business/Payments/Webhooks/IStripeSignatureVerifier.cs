using Stripe;

namespace eTicketing.Payment.Business.Payments.Webhooks;

/// <summary>
/// Wraps Stripe's EventUtility.ConstructEvent so the webhook service can be unit-tested.
///
/// Without this seam a test would have to compute a real HMAC over a real payload with a real
/// signing secret just to exercise "a duplicate delivery mints nothing" -- the interesting part.
/// With it, tests hand StripeWebhookService a hand-built Stripe.Event and the signature check
/// itself is verified once, against the library, in the manual test plan.
/// </summary>
public interface IStripeSignatureVerifier
{
    /// <summary>Verifies the Stripe-Signature header against the RAW request body and returns the
    /// parsed event, or null when the signature does not check out.</summary>
    Event? Verify(string rawBody, string? signatureHeader, string webhookSecret);
}
