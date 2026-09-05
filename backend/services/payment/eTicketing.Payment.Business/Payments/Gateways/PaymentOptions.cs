namespace eTicketing.Payment.Business.Payments.Gateways;

/// <summary>
/// Bound from the "Payment" configuration section. <see cref="Provider"/> selects which
/// <see cref="IPaymentGateway"/> is resolved at runtime and is the whole rollback story for the
/// Stripe integration: docs/arhitektura-migracija-mikroservisi-eda.md section 9 names Stripe the
/// second thing to sacrifice if time runs short, with an explicit fallback to the internal mock.
/// Setting PAYMENT_PROVIDER=Mock restores the pre-Stripe behavior with no code change and no
/// frontend rebuild — the resolved provider name travels back to the clients on every
/// PaymentIntentResponse, so they render the legacy card form on their own.
/// </summary>
public class PaymentOptions
{
    public const string SectionName = "Payment";

    /// <summary>"Mock" or "Stripe". Anything else (including empty) resolves to Mock, so a
    /// misconfigured deployment degrades to the offline gateway rather than failing to boot.</summary>
    public string Provider { get; set; } = PaymentProviderNames.Mock;

    public StripeOptions Stripe { get; set; } = new();
}

public static class PaymentProviderNames
{
    public const string Mock = "Mock";
    public const string Stripe = "Stripe";
}
