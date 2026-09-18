using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Data.Entities;

/// <summary>
/// A read model, not a domain entity: eTicketing.Catalog owns Product, this is Ticketing's local
/// copy of the handful of fields it has to be able to read *synchronously*.
///
/// <para>It exists because three of the hottest paths in this service need to know whether a
/// product is still published — the anonymous sector listing, the capacity hold, and the purchase
/// itself — and none of them can afford a cross-service HTTP call. Without it, a product that was
/// unpublished or deleted in Catalog goes on selling tickets in Ticketing indefinitely:
/// <c>Sector.Status</c> is written in exactly one place in the whole service
/// (<c>SectorService.PublishAsync</c>) and never by a consumer.</para>
///
/// <para>Fed by <c>product.changed</c> (upsert) and <c>product.deleted</c> (hard delete, like
/// everything else here), plus a startup backfill over the whitelisted Ticketing→Catalog call for
/// the rows that predate the projection. A missing row means "unknown" and is treated exactly like
/// "not published" — the safe direction: the worst case is a sector temporarily not listed, rather
/// than a deleted event still taking people's money.</para>
/// </summary>
public class ProductSnapshot : BaseEntity
{
    /// <summary>The primary key. One row per product, so a redelivery overwrites rather than
    /// duplicates, and the projection needs no surrogate id of its own.</summary>
    public Guid ProductId { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Null for DailyEntry and RecurringReservation products, which have no single date.</summary>
    public DateTime? Date { get; set; }

    public City City { get; set; }

    public PublishStatus Status { get; set; }

    public TicketingMode TicketingMode { get; set; }

    /// <summary>When Catalog produced the state this row holds — copied from the event, never from
    /// the local clock. It is what makes the projection safe under at-least-once delivery: an event
    /// older than this is dropped instead of being allowed to resurrect a stale status.
    /// <c>UpdatedAt</c> cannot do this job; it records when *this database* last wrote the row.</summary>
    public DateTime ChangedAt { get; set; }
}
