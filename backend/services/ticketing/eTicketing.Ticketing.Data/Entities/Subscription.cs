using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Data.Entities;

public enum SubscriptionStatus
{
    Active,
    Cancelled,
    PastDue
}

/// <summary>
/// Tracks a RecurringReservation Sector's (e.g. one parking space) ongoing reservation by a buyer.
///
/// Renewal is not a job on this side: the payment provider charges the saved card on its own monthly
/// schedule and tells us afterwards, so the period below advances when a subscription.renewed event
/// arrives (see SubscriptionRenewalService), never on a timer here.
/// </summary>
public class Subscription : BaseEntity
{
    public Guid Id { get; set; }

    public Guid SectorId { get; set; }
    public Sector? Sector { get; set; }

    // Cross-service ref to Identity.User — plain Guid column, no FK.
    public Guid UserId { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    public DateOnly CurrentPeriodStart { get; set; }
    public DateOnly CurrentPeriodEnd { get; set; }

    // When the (future) background renewal job should next attempt a charge for this subscription.
    public DateTime? NextRenewalAt { get; set; }

    // Opaque provider subscription reference ("sub_..."), returned by eTicketing.Payment at first
    // purchase — never raw card data, keeps PCI-relevant surface inside Payment. Every renewal,
    // failure and cancellation webhook is keyed by this, hence the unique index in
    // SubscriptionConfiguration.
    public string? PaymentReference { get; set; }

    /// <summary>
    /// The buyer's email, copied at purchase. A renewal mints its Ticket weeks later, from a webhook
    /// with no signed-in user anywhere in the request, so the address has to be on the subscription
    /// rather than looked up from the previous period's ticket.
    /// </summary>
    public string? UserEmail { get; set; }

    /// <summary>
    /// Set when the buyer cancels. Status deliberately stays Active until the provider actually ends
    /// the subscription at period end: they paid for this month and their ticket stays valid for it.
    /// </summary>
    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>
    /// The Redis hold that was permanently confirmed for this space at first purchase.
    ///
    /// Needed because releasing a confirmed hold requires the hold id: the capacity counter is a hash
    /// keyed by hold id, and ConfirmAsync deletes the holdinfo pointer that would otherwise let
    /// ReleaseAsync find it. Without this column a cancelled parking subscription would leave its
    /// space sold forever. See ISectorCapacityLock.ReleaseConfirmedAsync.
    /// </summary>
    public string? CapacityHoldId { get; set; }

    public DateTime? CancelledAt { get; set; }
}
