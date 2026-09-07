using eTicketing.Contracts.Persistence;

namespace eTicketing.Payment.Data.Entities;

/// <summary>
/// Serialized as its integer ordinal (System.Text.Json's default, no converter on either side) and
/// deserialized straight back into eTicketing.Ticketing's separately-declared PaymentChargeStatus.
/// The two enums are therefore one wire format in two files: APPEND ONLY, never reorder, never
/// remove. PaymentEnumParityTests fails the build the moment they drift.
/// </summary>
public enum PaymentStatus
{
    Succeeded,
    Failed,

    /// <summary>An intent exists at the provider and the buyer has not finished paying yet, or has
    /// authorized but not been captured. Rows sit here between POST /purchases/payment-intent and
    /// POST /purchases.</summary>
    Pending,

    Refunded,

    /// <summary>The authorization was voided without ever being captured, so no money moved. This
    /// is the normal outcome when a buyer confirms just as their Redis hold expires.</summary>
    Cancelled,
}

public enum PaymentProvider
{
    Mock,
    Stripe,
}

/// <summary>
/// One row per order's payment attempt. Card data never reaches this service: with the Stripe
/// provider the buyer's card goes straight from the browser to Stripe and only identifiers come
/// back, and with the Mock provider nothing card-shaped exists at all beyond the simulated last
/// four digits used to trigger a demo decline (see MockPaymentGateway).
/// </summary>
public class Payment : BaseEntity
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }

    // Matches the OrderId eTicketing.Ticketing's PurchaseService mints for the corresponding order.
    public string OrderRef { get; set; } = string.Empty;

    /// <summary>ISO-4217, lowercase, as the provider charges in. Stored per row rather than read
    /// from configuration at display time so historic rows stay truthful after the configured
    /// currency changes.</summary>
    public string Currency { get; set; } = "eur";

    public PaymentProvider Provider { get; set; }

    /// <summary>The buyer, carried so a capture can verify the intent belongs to the caller. Cross-
    /// service ref to Identity.User: plain Guid column, no FK, same convention as everywhere else.</summary>
    public Guid? UserId { get; set; }

    /// <summary>"pi_..." for Stripe, "pi_mock_..." for the mock. Unique where present.</summary>
    public string? ProviderPaymentIntentId { get; set; }

    /// <summary>"sub_..." on the RecurringReservation path. Also what a renewal webhook resolves
    /// against, hence the index.</summary>
    public string? ProviderSubscriptionId { get; set; }

    /// <summary>Stripe's decline_code (or error code), kept so the clients can show a specific
    /// Bosnian message rather than one generic failure text.</summary>
    public string? FailureCode { get; set; }
}
