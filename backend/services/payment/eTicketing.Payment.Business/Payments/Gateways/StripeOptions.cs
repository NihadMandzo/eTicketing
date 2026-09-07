namespace eTicketing.Payment.Business.Payments.Gateways;

/// <summary>
/// Bound from "Payment:Stripe" — the key name docs/payment-setup-guide.md section 2.2 pre-chose.
/// Every value arrives from the root .env through docker-compose; appsettings.json only ever
/// carries the "REPLACE_WITH_ENV_VARIABLE_IN_PRODUCTION" placeholder, same as Jwt:SigningKey.
/// </summary>
public class StripeOptions
{
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Safe to hand to a browser by definition. It is returned to the clients on the
    /// payment-intent response rather than baked into environment.ts / a --dart-define, so
    /// rotating the key is an .env edit plus a container restart — no web or mobile rebuild.</summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>The "whsec_..." secret that signs webhook deliveries. When testing locally this is
    /// the value `stripe listen` prints, NOT the one shown in the Stripe dashboard.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>ISO-4217, lowercase, as Stripe expects it. Defaults to "eur" rather than "bam"
    /// because a Stripe test account can only present the currencies its country allows, and BAM
    /// is unavailable on most of them — the UI keeps displaying KM either way. Verify with
    /// `stripe payment_intents create --amount 1000 --currency bam` before switching.</summary>
    public string Currency { get; set; } = "eur";
}
