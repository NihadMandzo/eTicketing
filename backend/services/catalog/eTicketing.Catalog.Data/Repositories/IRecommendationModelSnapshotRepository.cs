using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Repositories;

public interface IRecommendationModelSnapshotRepository : IRepository<RecommendationModelSnapshot, Guid>
{
    /// <summary>The model currently in use, or null when nothing has ever been trained — the
    /// expected state on a first boot against an empty database, not an error.</summary>
    Task<RecommendationModelSnapshot?> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Newest-first training history for the back-office screen.</summary>
    Task<List<RecommendationModelSnapshot>> GetRecentAsync(int take, CancellationToken ct = default);

    /// <summary>Inserts <paramref name="snapshot"/> as the active model and clears the flag on
    /// whatever held it before, so "exactly one active row" holds without needing a filtered unique
    /// index (see RecommendationModelSnapshotConfiguration). Does NOT commit — the calling service
    /// method owns the single SaveChangesAsync.</summary>
    Task AddAndActivateAsync(RecommendationModelSnapshot snapshot, CancellationToken ct = default);
}
