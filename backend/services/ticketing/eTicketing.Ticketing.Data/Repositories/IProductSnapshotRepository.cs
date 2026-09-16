using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Data.Repositories;

public interface IProductSnapshotRepository : IRepository<ProductSnapshot, Guid>
{
    /// <summary>Read-only sibling of the inherited <c>GetByIdAsync</c>, for the three hot paths
    /// that only ask "is this product still published?" — the sector listing, the capacity hold and
    /// the purchase. None of them writes the row back, so none of them should pay for tracking.
    /// </summary>
    Task<ProductSnapshot?> GetByIdNoTrackingAsync(Guid productId, CancellationToken ct = default);

    /// <summary>Batch read for the startup backfill, which resolves many products at once.</summary>
    Task<List<ProductSnapshot>> GetByIdsNoTrackingAsync(IReadOnlyList<Guid> productIds, CancellationToken ct = default);

    /// <summary>Every product this service holds a sector for that has no snapshot row yet — the
    /// backfill's work list. Distinct, and computed in SQL rather than by pulling both tables into
    /// memory to subtract them.
    ///
    /// <para>It answers with the products Ticketing actually cares about, not everything in the
    /// catalogue: a product with no sectors here sells nothing here, so a missing row for it costs
    /// nothing until its first sector arrives — and creating that sector fills the row in passing
    /// (see SectorService.ValidateAsync).</para></summary>
    Task<List<Guid>> GetProductIdsMissingSnapshotAsync(CancellationToken ct = default);
}
