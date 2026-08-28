using eTicketing.Catalog.Business.Recommendations;
using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;

namespace eTicketing.Catalog.Api.Endpoints;

public static class RecommendationEndpoints
{
    public static void MapRecommendationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/recommendations").WithTags("Recommendations");

        // Bare RequireAuthorization(), not a "Customer" policy — that policy doesn't exist in
        // AddPlatformAuthorizationPolicies, and every other buyer-scoped endpoint in the platform
        // (POST /purchases, GET /tickets/mine) authorizes the same way. It also happens to be the
        // behavior you want: an organizer browsing the storefront on their own account gets
        // recommendations exactly like any other signed-in visitor.
        group.MapGet("/me", GetForMe).RequireAuthorization().WithValidation<RecommendationQuery>();
        group.MapPost("/views", TrackView).RequireAuthorization().WithValidation<TrackViewRequest>();

        // Anonymous: neither needs a user history, and both run on the public storefront before
        // anyone has logged in.
        group.MapGet("/similar/{productId:guid}", GetSimilar).AllowAnonymous().WithValidation<SimilarProductsQuery>();
        group.MapGet("/popular", GetPopular).AllowAnonymous().WithValidation<PopularProductsQuery>();

        // Back-office: model state and a manual retrain, for the desktop "Preporuke" screen.
        group.MapGet("/status", GetStatus).RequireAuthorization("PlatformStaff");
        group.MapPost("/retrain", Retrain).RequireAuthorization("PlatformStaff");
    }

    private static async Task<IResult> GetForMe(
        [AsParameters] RecommendationQuery query, IRecommendationService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetForMeAsync(query, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> TrackView(
        TrackViewRequest request, IRecommendationService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.TrackViewAsync(request, http.User, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> GetSimilar(
        Guid productId, [AsParameters] SimilarProductsQuery query, IRecommendationService service, CancellationToken ct)
    {
        var result = await service.GetSimilarAsync(productId, query, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetPopular(
        [AsParameters] PopularProductsQuery query, IRecommendationService service, CancellationToken ct)
    {
        var result = await service.GetPopularAsync(query, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetStatus(IRecommendationService service, CancellationToken ct)
    {
        var result = await service.GetStatusAsync(ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Retrain(IRecommendationService service, CancellationToken ct)
    {
        var result = await service.RetrainAsync(ct);
        return result.ToHttpResult();
    }
}
