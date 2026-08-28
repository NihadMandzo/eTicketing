using System.Security.Claims;
using eTicketing.Catalog.Business.Products;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;

namespace eTicketing.Catalog.Business.Recommendations;

public interface IRecommendationService
{
    /// <summary>Records that the caller opened a product. Fire-and-forget from the frontends'
    /// point of view, but still validated: an unknown or Draft product is a NotFound, so this
    /// can't be used to probe unpublished inventory by watching which ids succeed.</summary>
    Task<Result> TrackViewAsync(TrackViewRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>The caller's personalized list, falling through Personalized → ContentBased →
    /// Popular depending on how much history they have and whether a model is loaded. Never fails
    /// for lack of data — a brand-new account gets the popular list, not an empty one.</summary>
    Task<Result<RecommendationResponse>> GetForMeAsync(RecommendationQuery query, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Products similar to one product, for anonymous and signed-in visitors alike (it
    /// needs no user history). Co-interaction first, padded with content similarity.</summary>
    Task<Result<List<ProductResponse>>> GetSimilarAsync(Guid productId, SimilarProductsQuery query, CancellationToken ct = default);

    /// <summary>Most-bought published products, optionally within one city. Public.</summary>
    Task<Result<List<ProductResponse>>> GetPopularAsync(PopularProductsQuery query, CancellationToken ct = default);

    /// <summary>Training state and interaction totals — backs the desktop back-office screen.</summary>
    Task<Result<ModelStatusResponse>> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Retrains immediately rather than waiting for the nightly run. Returns a Conflict
    /// when there is nothing to train on, which is the honest answer on an empty database — a
    /// model fitted to zero interactions would "succeed" and then score nothing.</summary>
    Task<Result<ModelStatusResponse>> RetrainAsync(CancellationToken ct = default);

    /// <summary>Called once at startup: brings the last trained model back from blob storage so a
    /// restarted container keeps recommending instead of starting cold, and only trains from
    /// scratch when there is nothing to load. This is the whole reason a training run is persisted
    /// rather than held in process memory.</summary>
    Task EnsureModelLoadedAsync(CancellationToken ct = default);
}
