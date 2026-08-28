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
    Task<PagedResult<Sector>> SearchAsync(
        BaseSearchObject query, Guid? productId, Guid? organizationId, PublishStatus? status, CancellationToken ct = default);

    /// <summary>Plain GetByIdAsync (DbSet.FindAsync) can't eager-load — use this instead wherever
    /// the caller needs Sector.TicketTypes populated in the response (Publish/Update; Purchase's
    /// own Sector lookup).</summary>
    Task<Sector?> GetByIdWithTicketTypesAsync(Guid id, CancellationToken ct = default);

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

/// <summary>Projection, not an entity — one row per product with its total published capacity and
/// how many sectors that came from (the "N sektora" half of the organizer-facing product meta).</summary>
public record ProductCapacity(Guid ProductId, int Capacity, int SectorCount);
