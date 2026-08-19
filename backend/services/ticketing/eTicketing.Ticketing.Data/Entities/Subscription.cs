using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Data.Entities;

public enum SubscriptionStatus
{
    Active,
    Cancelled,
    PastDue
}

/// <summary>
/// Schema-only this pass — no CRUD endpoints exist yet (buying/renewal is deferred, see
/// .claude/rules/01-domain.md). Tracks a RecurringReservation Sector's (e.g. one parking space)
/// ongoing reservation by a buyer, so a future simplified auto-renewal job has a data model to
/// work against without a second migration.
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

    // Opaque token returned by eTicketing.Payment at first purchase (e.g. a mock/Stripe customer +
    // payment-method reference) — never raw card data, keeps PCI-relevant surface inside Payment.
    public string? PaymentReference { get; set; }

    public DateTime? CancelledAt { get; set; }
}
