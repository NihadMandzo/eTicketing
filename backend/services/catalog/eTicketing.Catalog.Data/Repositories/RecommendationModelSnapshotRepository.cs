using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Catalog.Data.Repositories;

public class RecommendationModelSnapshotRepository
    : Repository<RecommendationModelSnapshot, Guid>, IRecommendationModelSnapshotRepository
{
    public RecommendationModelSnapshotRepository(CatalogDbContext context) : base(context) { }

    public Task<RecommendationModelSnapshot?> GetActiveAsync(CancellationToken ct = default)
        => Query().AsNoTracking().FirstOrDefaultAsync(s => s.IsActive, ct);

    public Task<List<RecommendationModelSnapshot>> GetRecentAsync(int take, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .OrderByDescending(s => s.TrainedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task AddAndActivateAsync(RecommendationModelSnapshot snapshot, CancellationToken ct = default)
    {
        // Tracked, so the flag clear is picked up by the same SaveChangesAsync that inserts the new
        // row — the two must land together or the "exactly one active" invariant breaks.
        var previouslyActive = await Query().Where(s => s.IsActive).ToListAsync(ct);
        foreach (var stale in previouslyActive)
        {
            stale.IsActive = false;
        }

        snapshot.IsActive = true;
        await AddAsync(snapshot, ct);
    }
}
