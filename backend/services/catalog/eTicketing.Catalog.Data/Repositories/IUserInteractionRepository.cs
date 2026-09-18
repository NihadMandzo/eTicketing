using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Repositories;

public interface IUserInteractionRepository : IRepository<UserInteraction, Guid>
{
    /// <summary>Records one occurrence of (user, product, type): inserts the row on first sight,
    /// increments <see cref="UserInteraction.Count"/> afterwards. Does NOT commit — the calling
    /// service method owns the single SaveChangesAsync, per .claude/rules/10-backend.md.
    ///
    /// The read and the write are not atomic, so two concurrent callers with the same key can both
    /// decide to insert and the loser's commit will be rejected by the unique index. Callers must
    /// go through InteractionRecordingExtensions.RecordOccurrenceAsync, which recovers from that
    /// rather than letting it surface as a 500.</summary>
    Task UpsertAsync(Guid userId, Guid productId, InteractionType type, DateTime occurredAt, CancellationToken ct = default);

    /// <summary>Detaches the not-yet-committed insert a losing <see cref="UpsertAsync"/> queued, so
    /// the caller can retry its save. Exists only for that recovery path — see
    /// InteractionRecordingExtensions.RecordOccurrenceAsync.</summary>
    void DiscardPendingInsert();

    /// <summary>Every interaction in the system, weighted, as the trainer wants it. Deliberately
    /// unpaged and untracked: matrix factorization trains on the whole matrix at once, and there is
    /// no meaningful way to train on "page 3".</summary>
    Task<List<InteractionTrainingRow>> GetTrainingRowsAsync(CancellationToken ct = default);

    /// <summary>One user's whole history — drives both the personalized ranking and the
    /// already-bought exclusion.</summary>
    Task<List<UserInteraction>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Product ids ranked by how many distinct people bought them, Published only. Backs
    /// the "Popularno" fallback (city-scoped only when the caller names a city — the fallback for a
    /// user with no history cannot), and the tail-padding of every other path.</summary>
    Task<List<Guid>> GetPopularProductIdsAsync(City? city, int take, CancellationToken ct = default);

    /// <summary>"People who touched this product also touched these" — product ids ranked by how
    /// many distinct users they share with <paramref name="productId"/>, excluding the product
    /// itself and anything unpublished. The collaborative half of "Slično ovome"; returns empty
    /// (not an error) for a product nobody has interacted with yet.</summary>
    Task<List<Guid>> GetCoInteractedProductIdsAsync(Guid productId, int take, CancellationToken ct = default);

    Task<InteractionStats> GetStatsAsync(CancellationToken ct = default);
}
