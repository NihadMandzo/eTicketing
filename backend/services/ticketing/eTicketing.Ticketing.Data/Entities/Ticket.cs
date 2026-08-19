using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Data.Entities;

public enum TicketStatus
{
    Processing,
    Confirmed,
    Ready,     // PDF generated (Sprint 4 flow)
    Cancelled
}

/// <summary>
/// Schema-only this pass — no Purchase endpoint exists yet (buying is deferred, see
/// .claude/rules/01-domain.md), so this table stays empty until that work lands. Defined now so
/// no second migration is needed once purchasing is built.
/// </summary>
public class Ticket : BaseEntity
{
    public Guid Id { get; set; }

    public Guid SectorId { get; set; }
    public Sector? Sector { get; set; }

    // Denormalized from Sector.ProductId — avoids a join for common "my tickets" filters and
    // matches the shape of the TicketPurchased integration event.
    public Guid ProductId { get; set; }

    // Cross-service ref to Identity.User — plain Guid column, no FK.
    public Guid UserId { get; set; }

    // Denormalized at purchase time for email/PDF even if the user record later changes.
    public string UserEmail { get; set; } = string.Empty;

    public TicketStatus Status { get; set; } = TicketStatus.Processing;
    public decimal PricePaid { get; set; }

    // DailyEntry only: which specific calendar date (within Sector.PeriodYear/PeriodMonth) this
    // ticket admits entry for.
    public DateOnly? ValidDate { get; set; }

    // RecurringReservation only: which billing period this specific ticket instance covers. The
    // first manual purchase creates the Subscription + this first Ticket; each later auto-renewal
    // mints one more Ticket linked to the same Subscription.
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public Guid? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }

    // Set once eTicketing.PdfGeneration finishes, Status → Ready.
    public string? PdfBlobName { get; set; }
}
