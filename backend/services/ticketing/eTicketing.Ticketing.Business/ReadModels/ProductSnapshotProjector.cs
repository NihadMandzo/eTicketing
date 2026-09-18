using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.ReadModels;

public class ProductSnapshotProjector : IProductSnapshotProjector
{
    /// <summary>Upper bound on one internal by-ids lookup, the same cap ReportService respects and
    /// the same one Catalog's ProductService enforces on the other side — exceed it and the answer
    /// is a 400 that surfaces here as an opaque 500.</summary>
    private const int MaxIdsPerLookup = 200;

    private readonly IProductSnapshotRepository _snapshots;
    private readonly ICatalogClient _catalogClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProductSnapshotProjector> _logger;

    public ProductSnapshotProjector(
        IProductSnapshotRepository snapshots,
        ICatalogClient catalogClient,
        IUnitOfWork unitOfWork,
        ILogger<ProductSnapshotProjector> logger)
    {
        _snapshots = snapshots;
        _catalogClient = catalogClient;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ApplyAsync(ProductSnapshotChanged message, CancellationToken ct = default)
    {
        var existing = await _snapshots.GetByIdAsync(message.ProductId, ct);

        if (existing is not null && existing.ChangedAt > message.ChangedAt)
        {
            // An older state arriving after a newer one — a redelivery, or two edits whose messages
            // crossed. Applying it would silently put back a status the organizer has already
            // changed, which on this table means a published product going unbuyable.
            _logger.LogInformation(
                "Zastarjeli snapshot za proizvod {ProductId} ({MessageChangedAt} < {StoredChangedAt}) — preskačem.",
                message.ProductId, message.ChangedAt, existing.ChangedAt);
            return;
        }

        if (existing is null)
        {
            await _snapshots.AddAsync(ToEntity(message), ct);
        }
        else
        {
            CopyInto(message, existing);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid productId, CancellationToken ct = default)
    {
        var existing = await _snapshots.GetByIdAsync(productId, ct);
        if (existing is null)
        {
            // Already gone, or never projected. Nothing to undo and nothing to report: a delete
            // that finds no row has still achieved what it was asked to.
            return;
        }

        _snapshots.Remove(existing);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task EnsureAsync(ProductSnapshotChanged snapshot, CancellationToken ct = default)
    {
        var existing = await _snapshots.GetByIdAsync(snapshot.ProductId, ct);
        if (existing is not null)
            return;

        await _snapshots.AddAsync(ToEntity(snapshot), ct);
        // No SaveChangesAsync — the caller's own save commits this, so the snapshot and whatever
        // the caller was doing share one transaction.
    }

    public async Task<int> BackfillAsync(CancellationToken ct = default)
    {
        var missing = await _snapshots.GetProductIdsMissingSnapshotAsync(ct);
        if (missing.Count == 0)
            return 0;

        var written = 0;

        for (var offset = 0; offset < missing.Count; offset += MaxIdsPerLookup)
        {
            var batch = missing.Skip(offset).Take(MaxIdsPerLookup).ToList();
            var products = await _catalogClient.GetProductsAsync(batch, ct);

            foreach (var product in products)
            {
                await _snapshots.AddAsync(ToEntity(FromCatalog(product, DateTime.UtcNow)), ct);
                written++;
            }

            // An id Catalog does not answer for is a product that no longer exists — its sectors
            // stay unlistable, which is exactly right. Logged rather than retried: no amount of
            // asking again will bring a deleted product back.
            var unknown = batch.Count - products.Count;
            if (unknown > 0)
            {
                _logger.LogWarning(
                    "{Count} proizvoda iz sektora ove usluge ne postoji više u katalogu — njihovi sektori ostaju nedostupni.",
                    unknown);
            }
        }

        if (written > 0)
            await _unitOfWork.SaveChangesAsync(ct);

        return written;
    }

    /// <summary>The snapshot as Catalog's internal product read describes it. Shared by the
    /// backfill and by SectorService's opportunistic fill, which both have a
    /// <see cref="CatalogProductResponse"/> rather than an event in hand.</summary>
    public static ProductSnapshotChanged FromCatalog(CatalogProductResponse product, DateTime readAt) =>
        new(product.Id, product.OrganizationId, product.Name, product.Date, product.City,
            product.Status, product.TicketingMode, readAt);

    private static ProductSnapshot ToEntity(ProductSnapshotChanged message)
    {
        var snapshot = new ProductSnapshot { ProductId = message.ProductId };
        CopyInto(message, snapshot);
        return snapshot;
    }

    private static void CopyInto(ProductSnapshotChanged message, ProductSnapshot snapshot)
    {
        snapshot.OrganizationId = message.OrganizationId;
        snapshot.Name = message.Name;
        snapshot.Date = message.Date;
        snapshot.City = message.City;
        snapshot.Status = message.Status;
        snapshot.TicketingMode = message.TicketingMode;
        snapshot.ChangedAt = message.ChangedAt;
    }
}
