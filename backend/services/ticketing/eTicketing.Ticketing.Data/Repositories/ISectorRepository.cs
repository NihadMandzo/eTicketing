using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Data.Repositories;

public interface ISectorRepository : IRepository<Sector, Guid>
{
    /// <summary>Paged, optionally narrowed by product, organization and/or status — backs
    /// SectorService.GetPublishedAsync/GetMineAsync/GetAllAsync. Parameters are passed plainly
    /// rather than the Business-layer SectorQuery type so Data doesn't need to reference
    /// Business.</summary>
    /// <param name="requirePublishedProduct">Also require a ProductSnapshot row that says the
    /// owning product is Published — the public buy path only. A sector's own Status says nothing
    /// about the product it belongs to, so without this a product that was unpublished or deleted
    /// in eTicketing.Catalog keeps its sectors on sale here.
    ///
    /// <para>Applied inside the query, not to the materialized page, because filtering afterwards
    /// would leave TotalCount counting rows that were then dropped — the paging contract in
    /// .claude/rules/01-domain.md is what both PaginationBar widgets read.</para>
    /// <para>The back-office listings pass false: an organizer configuring sectors for a product
    /// they have not published yet must still see them.</para></param>
    Task<PagedResult<Sector>> SearchAsync(
        BaseSearchObject query, Guid? productId, Guid? organizationId, PublishStatus? status,
        bool requirePublishedProduct, CancellationToken ct = default);

    /// <summary>Plain GetByIdAsync (DbSet.FindAsync) can't eager-load — use this instead wherever
    /// the caller needs Sector.TicketTypes populated in the response (Publish/Update; Purchase's
    /// own Sector lookup).</summary>
    Task<Sector?> GetByIdWithTicketTypesAsync(Guid id, CancellationToken ct = default);

    /// <summary>A product's published sectors with their tiers, ordered by name — backs the
    /// organizer's ticket-print options screen. No-tracking: the caller only reads them to build a
    /// response.</summary>
    Task<List<Sector>> GetPublishedByProductWithTicketTypesAsync(Guid productId, CancellationToken ct = default);

    /// <summary>The batch sibling of <see cref="GetByIdWithTicketTypesAsync"/>, for resolving every
    /// sector named by a print batch in one query instead of one per line.
    ///
    /// **Tracked, deliberately** — unlike the other batch reads here. TicketPrintService hands these
    /// Sector instances to Ticket.ForPrint and saves the resulting tickets in the same
    /// SaveChangesAsync, so the change tracker has to already know them; AsNoTracking would make EF
    /// treat each one as a new row to insert.</summary>
    Task<List<Sector>> GetByIdsWithTicketTypesAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);

    /// <summary>Total published capacity per product — the denominator behind the Popunjenost
    /// column of GET /reports/products. Draft sectors are excluded: nobody could have bought into
    /// them, so counting their seats would depress every occupancy figure.
    ///
    /// The number is only meaningful for SingleOccurrence and RecurringReservation, where capacity
    /// is a fixed total. For DailyEntry it is a per-day allowance repeated across a month, which
    /// has no single denominator — the caller (ReportService) knows the mode and renders those
    /// products without an occupancy figure rather than dividing by the wrong thing.</summary>
    Task<List<ProductCapacity>> GetPublishedCapacityByProductAsync(
        IReadOnlyList<Guid> productIds, CancellationToken ct = default);
}
