using eTicketing.Catalog.Data.Repositories;

namespace eTicketing.Catalog.Business.Recommendations;

/// <summary>What one completed training run produced, handed back so the caller can persist it as
/// a RecommendationModelSnapshot. The model itself has already been uploaded by the time this
/// returns — this is the metadata about it.</summary>
public record TrainingOutcome(string BlobName, int InteractionCount, int UserCount, int ProductCount, int TrainingDurationMs);

/// <summary>
/// The collaborative-filtering model, held as a singleton because it is expensive to build and
/// shared by every request. Implementations must be safe to call from several requests at once
/// while a retrain is running.
///
/// Deliberately narrow: it knows about (user, product, weight) triples and nothing else — no
/// categories, no cities, no dates. Everything content-aware lives in RecommendationService, which
/// is also what runs when this returns false.
/// </summary>
public interface IRecommendationModel
{
    /// <summary>False until a model has been trained or loaded from storage. Every caller must
    /// check it (or check <see cref="TryScore"/>'s return) rather than assuming a model exists —
    /// on a first boot against an empty database, none does.</summary>
    bool IsTrained { get; }

    /// <summary>Trains on the whole interaction matrix and uploads the result to blob storage.
    /// The new model replaces the live one only once training has fully succeeded, so a failed
    /// retrain leaves the previous model serving rather than leaving the service with none.</summary>
    Task<TrainingOutcome> TrainAsync(IReadOnlyList<InteractionTrainingRow> rows, CancellationToken ct = default);

    /// <summary>Pulls a previously trained model back out of blob storage — how a restarted
    /// container picks up where it left off instead of starting cold. Returns false if the blob is
    /// gone (the snapshot row outlived the artifact), which the caller should treat as "retrain",
    /// not as a crash.</summary>
    Task<bool> LoadAsync(string blobName, CancellationToken ct = default);

    /// <summary>Predicted affinity of <paramref name="userId"/> for <paramref name="productId"/>.
    /// False — not a zero score — when no model is loaded, or when either id was absent from the
    /// training data and the model therefore has no opinion at all. Distinguishing "unknown" from
    /// "scored low" is what lets the caller fall through to the content-based path instead of
    /// burying a new user's recommendations at the bottom of the list.</summary>
    bool TryScore(Guid userId, Guid productId, out float score);
}
