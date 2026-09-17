using eTicketing.Contracts.Events;

namespace eTicketing.Ticketing.Business.ReadModels;

/// <summary>
/// Keeps Ticketing's ProductSnapshot table in step with eTicketing.Catalog.
///
/// <para>Every method here is safe to run twice. Deliveries are at-least-once, the broker gives no
/// ordering guarantee across a redelivery, and a startup backfill will happily race a live event —
/// so "apply this state if it is newer than what I hold" is the only rule the projection needs, and
/// the only one it enforces.</para>
/// </summary>
public interface IProductSnapshotProjector
{
    /// <summary>Handles <c>product.changed</c>: upsert, then commit. This is the top of a message
    /// handler, so it owns its own <c>SaveChangesAsync</c>.</summary>
    Task ApplyAsync(ProductSnapshotChanged message, CancellationToken ct = default);

    /// <summary>The projection half of <c>product.deleted</c>: hard-delete the row, then commit.
    /// Deleting it is what makes the product's sectors unlistable and unholdable — see
    /// ProductSnapshot for why that had to happen here rather than by rewriting Sector.Status.
    /// </summary>
    Task RemoveAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// Records a snapshot for a product that has none yet, <b>without</b> saving — for a caller
    /// that already holds the product's state and has its own single <c>SaveChangesAsync</c>
    /// coming (SectorService, which reads the product over the whitelisted Ticketing→Catalog call
    /// on every sector create/update).
    ///
    /// <para>Deliberately insert-if-missing rather than a true upsert: this data came from an HTTP
    /// read with no idea when Catalog produced it, so letting it overwrite a live row could put a
    /// stale status back. Filling a gap is always an improvement; overwriting is not.</para>
    /// </summary>
    Task EnsureAsync(ProductSnapshotChanged snapshot, CancellationToken ct = default);

    /// <summary>
    /// Fills in snapshots for products this service already holds sectors for but has never
    /// received an event about — rows that predate the projection, or were lost while the consumer
    /// was down. Reads them over the whitelisted Ticketing→Catalog call and commits.
    ///
    /// <para>Runs at startup and periodically, which makes it self-healing rather than a one-off
    /// migration step. Returns how many rows it wrote.</para>
    /// </summary>
    Task<int> BackfillAsync(CancellationToken ct = default);
}
