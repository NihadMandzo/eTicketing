using eTicketing.Contracts.Persistence;

namespace eTicketing.Payment.Data.Entities;

/// <summary>
/// Maps an eTicketing user to their Stripe customer, so a buyer with several parking subscriptions
/// has one customer and one saved card rather than one per subscription.
///
/// It lives here, in Payment, on purpose: eTicketing.Identity has no Stripe dependency and should
/// not gain one, and provider-specific identifiers are exactly what this service exists to keep
/// contained. Ticketing only ever sees the opaque subscription reference it stores on
/// Subscription.PaymentReference.
/// </summary>
public class StripeCustomer : BaseEntity
{
    public Guid Id { get; set; }

    /// <summary>Cross-service ref to Identity.User: plain Guid column, no FK. Unique.</summary>
    public Guid UserId { get; set; }

    /// <summary>Stripe's "cus_..." identifier.</summary>
    public string ProviderCustomerId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}
