namespace eTicketing.Catalog.Business.Recommendations;

/// <summary>Everything the desktop "Preporuke" screen shows. <paramref name="IsTrained"/> is the
/// live in-process fact (is a model loaded and scoring right now), while the rest describes the
/// last run — they disagree exactly when a snapshot exists but its blob could not be loaded, which
/// is precisely the situation worth being able to see.</summary>
public record ModelStatusResponse(
    DateTime? TrainedAt,
    bool IsTrained,
    int ModelInteractionCount,
    int ModelUserCount,
    int ModelProductCount,
    int TrainingDurationMs,
    int TotalInteractions,
    int ViewCount,
    int PurchaseCount,
    int DistinctUsers,
    int DistinctProducts,
    IReadOnlyList<ModelSnapshotResponse> History);
