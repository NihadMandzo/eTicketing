using System.Security.Claims;
using eTicketing.Catalog.Business.Products;
using eTicketing.Catalog.Business.Products.Mapping;
using eTicketing.Contracts.Security;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eTicketing.Catalog.Business.Recommendations;

/// <summary>
/// Ranks published products for a user, falling through three strategies in order of how much it
/// actually knows about them:
///
///   1. Personalized  — the trained matrix-factorization model has an opinion about this user.
///   2. ContentBased  — it doesn't, but the user has touched something, so rank by resemblance.
///   3. Popular       — the user is brand new; rank by what everyone else bought.
///
/// The fallback is not defensive padding, it is the design: a fresh deployment has no purchases and
/// a new account has no history, and a recommender that returns an empty row in either case is
/// worse than useless on a storefront. Every path is additionally padded from the one below it, so
/// a half-filled row never reaches the UI.
/// </summary>
public class RecommendationService : IRecommendationService
{
    private readonly IUserInteractionRepository _interactions;
    private readonly IProductRepository _products;
    private readonly IRecommendationModelSnapshotRepository _snapshots;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRecommendationModel _model;
    private readonly ProductResponseFactory _responseFactory;
    private readonly RecommendationOptions _options;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        IUserInteractionRepository interactions,
        IProductRepository products,
        IRecommendationModelSnapshotRepository snapshots,
        IUnitOfWork unitOfWork,
        IRecommendationModel model,
        ProductResponseFactory responseFactory,
        IOptions<RecommendationOptions> options,
        ILogger<RecommendationService> logger)
    {
        _interactions = interactions;
        _products = products;
        _snapshots = snapshots;
        _unitOfWork = unitOfWork;
        _model = model;
        _responseFactory = responseFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result> TrackViewAsync(TrackViewRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var product = await _products.GetByIdAsync(request.ProductId, ct);

        // Draft is deliberately a 404, not a 403: a caller who can't see a product on the public
        // listing must not be able to learn it exists by watching which ids this endpoint accepts.
        if (product is null || product.Status != PublishStatus.Published)
            return Result.Failure(Error.NotFound("product.not_found", "Proizvod nije pronađen."));

        await _interactions.RecordOccurrenceAsync(
            _unitOfWork, user.GetUserId(), request.ProductId, InteractionType.View, DateTime.UtcNow, ct);

        return Result.Success();
    }

    public async Task<Result<RecommendationResponse>> GetForMeAsync(
        RecommendationQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userId = user.GetUserId();
        var history = await _interactions.GetByUserAsync(userId, ct);

        var alreadyBought = history
            .Where(i => i.Type == InteractionType.Purchase)
            .Select(i => i.ProductId)
            .ToHashSet();

        var candidates = (await _products.GetPublishedCandidatesAsync(_options.MaxCandidates, ct))
            .Where(p => !alreadyBought.Contains(p.Id))
            .Where(IsStillOffered)
            .ToList();

        if (candidates.Count == 0)
            return Result<RecommendationResponse>.Success(new RecommendationResponse([], RecommendationSource.Popular));

        var source = ResolveSource(history.Count, userId, candidates);
        var ranked = source switch
        {
            RecommendationSource.Personalized => RankPersonalized(userId, candidates),
            RecommendationSource.ContentBased => RankContentBased(history, candidates),
            // Deliberately not city-scoped. This arm is reached only when the user has no history
            // at all (see ResolveSource), and Catalog cannot know where such a user is — the
            // profile address lives in Identity, which this service never calls. The clients title
            // the row "Popularno" rather than "Popularno u vašem gradu" for exactly that reason;
            // the only genuinely city-scoped list is GetPopularAsync, where the caller names it.
            _ => await RankPopularAsync(
                city: null, candidates, p => IsStillOffered(p) && !alreadyBought.Contains(p.Id), ct),
        };

        // Pad from the strategies below, in order, so a model that only recognises three of the
        // candidates still yields a full row rather than a stub.
        var padded = Pad(ranked, query.Take, [
            () => RankContentBased(history, candidates),
            () => candidates.OrderByDescending(p => p.CreatedAt).ToList(),
        ]);

        return Result<RecommendationResponse>.Success(
            new RecommendationResponse(_responseFactory.ToResponses(padded), source));
    }

    public async Task<Result<List<ProductResponse>>> GetSimilarAsync(
        Guid productId, SimilarProductsQuery query, CancellationToken ct = default)
    {
        var anchor = await _products.GetByIdWithCategoryAsync(productId, ct);
        if (anchor is null || anchor.Status != PublishStatus.Published)
            return Result<List<ProductResponse>>.Failure(Error.NotFound("product.not_found", "Proizvod nije pronađen."));

        // Collaborative half: other products the same people touched. Empty for a product nobody
        // has interacted with yet, which on a young catalog is most of them — hence the padding.
        var coInteractedIds = await _interactions.GetCoInteractedProductIdsAsync(productId, query.Take, ct);
        var ranked = InIdOrder(await _products.GetPublishedByIdsWithDetailsAsync(coInteractedIds, ct), coInteractedIds)
            .Where(IsStillOffered)
            .ToList();

        var candidates = (await _products.GetPublishedCandidatesAsync(_options.MaxCandidates, ct))
            .Where(p => p.Id != productId)
            .Where(IsStillOffered)
            .ToList();

        var padded = Pad(ranked, query.Take, [() => RankByResemblanceTo(anchor, candidates)]);

        return Result<List<ProductResponse>>.Success(_responseFactory.ToResponses(padded));
    }

    public async Task<Result<List<ProductResponse>>> GetPopularAsync(PopularProductsQuery query, CancellationToken ct = default)
    {
        var candidates = (await _products.GetPublishedCandidatesAsync(_options.MaxCandidates, ct))
            .Where(p => query.City is null || p.City == query.City)
            .Where(IsStillOffered)
            .ToList();

        var ranked = await RankPopularAsync(query.City, candidates, IsStillOffered, ct);

        return Result<List<ProductResponse>>.Success(_responseFactory.ToResponses(ranked.Take(query.Take).ToList()));
    }

    public async Task<Result<ModelStatusResponse>> GetStatusAsync(CancellationToken ct = default)
    {
        var active = await _snapshots.GetActiveAsync(ct);
        var history = await _snapshots.GetRecentAsync(HistoryLength, ct);
        var stats = await _interactions.GetStatsAsync(ct);

        return Result<ModelStatusResponse>.Success(new ModelStatusResponse(
            TrainedAt: active?.TrainedAt,
            IsTrained: _model.IsTrained,
            ModelInteractionCount: active?.InteractionCount ?? 0,
            ModelUserCount: active?.UserCount ?? 0,
            ModelProductCount: active?.ProductCount ?? 0,
            TrainingDurationMs: active?.TrainingDurationMs ?? 0,
            TotalInteractions: stats.TotalInteractions,
            ViewCount: stats.ViewCount,
            PurchaseCount: stats.PurchaseCount,
            DistinctUsers: stats.DistinctUsers,
            DistinctProducts: stats.DistinctProducts,
            History: history.Select(ToSnapshotResponse).ToList()));
    }

    public async Task<Result<ModelStatusResponse>> RetrainAsync(CancellationToken ct = default)
    {
        var rows = await _interactions.GetTrainingRowsAsync(ct);
        if (rows.Count == 0)
        {
            // Reported rather than silently "succeeding": matrix factorization fitted to an empty
            // matrix produces a model that recognises nobody, which would look trained on the
            // back-office screen while scoring nothing at all.
            return Result<ModelStatusResponse>.Failure(Error.Conflict(
                "recommendation.no_training_data", "Nema zabilježenih interakcija — model se ne može trenirati."));
        }

        var outcome = await _model.TrainAsync(rows, ct);

        await _snapshots.AddAndActivateAsync(new RecommendationModelSnapshot
        {
            Id = Guid.NewGuid(),
            BlobName = outcome.BlobName,
            TrainedAt = DateTime.UtcNow,
            InteractionCount = outcome.InteractionCount,
            UserCount = outcome.UserCount,
            ProductCount = outcome.ProductCount,
            TrainingDurationMs = outcome.TrainingDurationMs,
        }, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await GetStatusAsync(ct);
    }

    public async Task EnsureModelLoadedAsync(CancellationToken ct = default)
    {
        if (_model.IsTrained)
            return;

        var active = await _snapshots.GetActiveAsync(ct);
        if (active is not null && await _model.LoadAsync(active.BlobName, ct))
            return;

        // Either nothing has ever been trained (first boot) or the artifact is gone. Both are
        // fixed by training now — and doing so at startup is what makes seeded interaction data
        // produce real recommendations on the very first `docker compose up`.
        var result = await RetrainAsync(ct);
        if (result.IsFailure)
            _logger.LogInformation("Model preporuka nije treniran pri pokretanju: {Reason}", result.Error.Message);
    }

    // ─── Strategy selection ──────────────────────────────────────────────────────────────────

    private const int HistoryLength = 10;

    private RecommendationSource ResolveSource(int historyCount, Guid userId, List<Product> candidates)
    {
        if (historyCount == 0)
            return RecommendationSource.Popular;

        if (historyCount < _options.MinInteractionsForPersonalized || !_model.IsTrained)
            return RecommendationSource.ContentBased;

        // Having enough history isn't sufficient — the user may have accumulated it entirely since
        // the last nightly retrain, in which case the model has never seen them and TryScore
        // returns false for every candidate. Reporting Personalized then would be a lie.
        var modelKnowsThisUser = candidates.Any(p => _model.TryScore(userId, p.Id, out _));
        return modelKnowsThisUser ? RecommendationSource.Personalized : RecommendationSource.ContentBased;
    }

    private List<Product> RankPersonalized(Guid userId, List<Product> candidates) =>
        candidates
            .Select(p => (Product: p, Scored: _model.TryScore(userId, p.Id, out var score), Score: score))
            .Where(x => x.Scored)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Product)
            .ToList();

    /// <summary>
    /// Ranks by resemblance to everything the user has already touched. Category dominates (someone
    /// who buys concerts wants concerts), city comes next (nobody drives four hours for a museum
    /// day-pass), and ticketing mode last — it is the weakest of the three because it is almost
    /// implied by category.
    /// </summary>
    private static List<Product> RankContentBased(List<UserInteraction> history, List<Product> candidates)
    {
        var byCategory = new Dictionary<int, double>();
        var byCity = new Dictionary<City, double>();
        var byMode = new Dictionary<TicketingMode, double>();

        foreach (var interaction in history)
        {
            if (interaction.Product is null)
                continue;

            double weight = interaction.Count * (interaction.Type == InteractionType.Purchase
                ? UserInteraction.PurchaseWeightMultiplier
                : 1);

            Accumulate(byCategory, interaction.Product.CategoryId, weight);
            Accumulate(byCity, interaction.Product.City, weight);
            if (interaction.Product.Category is not null)
                Accumulate(byMode, interaction.Product.Category.TicketingMode, weight);
        }

        // Normalized to 0..1 each, so the 3/2/1 coefficients below actually express the intended
        // relative importance instead of being swamped by whichever dimension happens to have the
        // largest raw totals.
        var categoryScale = Scale(byCategory);
        var cityScale = Scale(byCity);
        var modeScale = Scale(byMode);

        return candidates
            .OrderByDescending(p =>
                3.0 * (byCategory.GetValueOrDefault(p.CategoryId) / categoryScale) +
                2.0 * (byCity.GetValueOrDefault(p.City) / cityScale) +
                1.0 * (p.Category is null ? 0 : byMode.GetValueOrDefault(p.Category.TicketingMode) / modeScale) +
                Imminence(p))
            .ToList();
    }

    /// <summary>Item-to-item resemblance, for "Slično ovome" where there is no user to profile.
    /// Same shape as the user version above, plus physical proximity — for two otherwise equal
    /// products the nearer venue is the better suggestion.</summary>
    private static List<Product> RankByResemblanceTo(Product anchor, List<Product> candidates) =>
        candidates
            .OrderByDescending(p =>
                (p.CategoryId == anchor.CategoryId ? 3.0 : 0.0) +
                (p.City == anchor.City ? 2.0 : 0.0) +
                (p.Category?.TicketingMode == anchor.Category?.TicketingMode ? 1.0 : 0.0) +
                Proximity(anchor, p) +
                Imminence(p))
            .ToList();

    /// <summary>
    /// Ranks by how many people bought each product, across the WHOLE published catalog — the ids
    /// come back ranked from SQL and are then hydrated by id, deliberately not intersected with
    /// <paramref name="candidates"/>. That intersection is what an earlier version did, and it
    /// silently dropped any best-seller older than the newest MaxCandidates products: a popularity
    /// list that can't reach the most-bought product in the catalog isn't a popularity list.
    /// <paramref name="candidates"/> is still the tail — which is the entire ranking on a catalog
    /// with no sales yet — and <paramref name="isEligible"/> re-applies the caller's exclusions
    /// (past events, products the user already bought) to the separately loaded popular half.
    /// </summary>
    private async Task<List<Product>> RankPopularAsync(
        City? city, List<Product> candidates, Func<Product, bool> isEligible, CancellationToken ct)
    {
        var popularIds = await _interactions.GetPopularProductIdsAsync(city, _options.MaxCandidates, ct);
        var mostBought = InIdOrder(await _products.GetPublishedByIdsWithDetailsAsync(popularIds, ct), popularIds)
            .Where(isEligible)
            .ToList();

        // Products nobody has bought sort after every product somebody has, newest first among
        // themselves.
        var alreadyRanked = mostBought.Select(p => p.Id).ToHashSet();
        return
        [
            .. mostBought,
            .. candidates.Where(p => !alreadyRanked.Contains(p.Id)).OrderByDescending(p => p.CreatedAt),
        ];
    }

    // ─── Shared helpers ──────────────────────────────────────────────────────────────────────

    /// <summary>Tops <paramref name="ranked"/> up to <paramref name="take"/> using each fallback in
    /// turn, skipping anything already present. Order within the primary ranking is preserved — the
    /// padding only ever appends.</summary>
    private static List<Product> Pad(List<Product> ranked, int take, IReadOnlyList<Func<List<Product>>> fallbacks)
    {
        var result = ranked.Take(take).ToList();
        var seen = result.Select(p => p.Id).ToHashSet();

        foreach (var fallback in fallbacks)
        {
            if (result.Count >= take)
                break;

            foreach (var product in fallback())
            {
                if (result.Count >= take)
                    break;
                if (seen.Add(product.Id))
                    result.Add(product);
            }
        }

        return result;
    }

    /// <summary>A SingleOccurrence product whose date has passed can't be bought any more, so
    /// recommending it is just noise. DailyEntry and RecurringReservation carry no product-level
    /// date — their availability is per sector — so they always pass.</summary>
    private static bool IsStillOffered(Product product) =>
        product.Category?.TicketingMode != TicketingMode.SingleOccurrence
        || product.Date is null
        || product.Date > DateTime.UtcNow;

    /// <summary>Small tie-breaker favouring events happening sooner. Capped well below the
    /// category/city coefficients so it orders equals rather than overriding relevance.</summary>
    private static double Imminence(Product product)
    {
        if (product.Date is null)
            return 0;

        var daysAway = (product.Date.Value - DateTime.UtcNow).TotalDays;
        return daysAway < 0 ? 0 : 0.5 / (1.0 + daysAway);
    }

    /// <summary>Great-circle distance folded into a 0..1 bonus (1 = same spot, →0 as it gets far).
    /// 50km is the half-way point — roughly "still the same trip".</summary>
    private static double Proximity(Product a, Product b)
    {
        const double earthRadiusKm = 6371.0;
        const double halfScoreDistanceKm = 50.0;

        var dLat = ToRadians(b.Latitude - a.Latitude);
        var dLon = ToRadians(b.Longitude - a.Longitude);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians(a.Latitude)) * Math.Cos(ToRadians(b.Latitude))
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var distanceKm = 2 * earthRadiusKm * Math.Asin(Math.Min(1.0, Math.Sqrt(h)));

        return halfScoreDistanceKm / (halfScoreDistanceKm + distanceKm);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static IEnumerable<Product> InIdOrder(List<Product> products, List<Guid> ids)
    {
        var rank = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        return products.OrderBy(p => rank.TryGetValue(p.Id, out var index) ? index : int.MaxValue);
    }

    private static void Accumulate<TKey>(Dictionary<TKey, double> totals, TKey key, double weight) where TKey : notnull =>
        totals[key] = totals.GetValueOrDefault(key) + weight;

    /// <summary>Largest value in the map, or 1 when it's empty — never 0, so callers can divide
    /// unconditionally.</summary>
    private static double Scale<TKey>(Dictionary<TKey, double> totals) where TKey : notnull =>
        totals.Count == 0 ? 1.0 : Math.Max(1.0, totals.Values.Max());

    private static ModelSnapshotResponse ToSnapshotResponse(RecommendationModelSnapshot snapshot) =>
        new(snapshot.Id, snapshot.TrainedAt, snapshot.InteractionCount, snapshot.UserCount,
            snapshot.ProductCount, snapshot.TrainingDurationMs, snapshot.IsActive);
}
