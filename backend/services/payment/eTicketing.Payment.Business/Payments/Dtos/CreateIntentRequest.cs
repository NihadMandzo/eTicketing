namespace eTicketing.Payment.Business.Payments;

/// <summary>
/// Creates the provider-side object the buyer confirms in their browser. Called by
/// eTicketing.Ticketing only, over the internal Docker network -- this endpoint has no Gateway route.
/// </summary>
public record CreateIntentRequest
{
    /// <summary>Computed server-side by Ticketing from the held sector and its ticket types. The
    /// client never states a price.</summary>
    public decimal Amount { get; init; }

    /// <summary>The order this pays for, and the idempotency key: unique index on Payments.OrderRef,
    /// provider idempotency key, and the metadata a later capture is verified against.</summary>
    public string OrderRef { get; init; } = string.Empty;

    public Guid UserId { get; init; }
    public string? CustomerEmail { get; init; }
    public string Description { get; init; } = string.Empty;

    /// <summary>The Redis hold this order is for, recorded as provider metadata so an intent can be
    /// traced back to the reservation that justified it.</summary>
    public string HoldRef { get; init; } = string.Empty;

    public Guid SectorId { get; init; }

    /// <summary>True for a RecurringReservation sector: creates a real recurring subscription whose
    /// first invoice the client confirms, rather than a one-off intent.</summary>
    public bool IsSubscription { get; init; }

    /// <summary>Shown on the buyer's card statement and in the provider dashboard. Required when
    /// IsSubscription is true.</summary>
    public string? SubscriptionProductName { get; init; }
}
