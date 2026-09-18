using Stripe;

namespace eTicketing.Payment.Business.Payments.Webhooks;

public class StripeSignatureVerifier : IStripeSignatureVerifier
{
    public Event? Verify(string rawBody, string? signatureHeader, string webhookSecret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(webhookSecret))
            return null;

        try
        {
            // throwOnApiVersionMismatch: false -- the account's configured webhook API version drifts
            // independently of the Stripe.net version pinned here, and a mismatch on an event we
            // parse successfully is not a reason to reject a legitimately signed delivery.
            return EventUtility.ConstructEvent(rawBody, signatureHeader, webhookSecret, 300, false);
        }
        catch (StripeException)
        {
            return null;
        }
    }
}
