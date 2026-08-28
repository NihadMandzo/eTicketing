using eTicketing.Catalog.Business.Products;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Business.Recommendations;

/// <summary>Which of the three ranking strategies actually produced a list. Persisted nowhere and
/// travelling on the wire deliberately: the frontends title the row from it ("Preporučeno za vas"
/// vs "Popularno"), and it makes the fallback chain observable instead of guesswork
/// when a demo shows something unexpected.
///
/// Serialized as the integer ordinal, like every other enum crossing this boundary — mirrored by
/// ordinal in the frontend enum tables. Append only.</summary>
public enum RecommendationSource
{
    /// <summary>Ranked by the trained matrix-factorization model.</summary>
    Personalized,

    /// <summary>The user has some history but not enough for the model (or the model has never
    /// seen them) — ranked by similarity to what they already touched.</summary>
    ContentBased,

    /// <summary>No usable history at all — ranked by how many people bought each product.</summary>
    Popular
}

public record RecommendationResponse(IReadOnlyList<ProductResponse> Items, RecommendationSource Source);

public record TrackViewRequest(Guid ProductId);

/// <summary>One past training run, newest first, for the back-office history table.</summary>
public record ModelSnapshotResponse(
    Guid Id, DateTime TrainedAt, int InteractionCount, int UserCount, int ProductCount, int TrainingDurationMs, bool IsActive);

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

public sealed record RecommendationQuery
{
    public int Take { get; init; } = 8;
}

public sealed record SimilarProductsQuery
{
    public int Take { get; init; } = 6;
}

public sealed record PopularProductsQuery
{
    public City? City { get; init; }
    public int Take { get; init; } = 8;
}
