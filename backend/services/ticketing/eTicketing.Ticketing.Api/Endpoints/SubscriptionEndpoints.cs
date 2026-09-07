using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Ticketing.Business.Subscriptions;

namespace eTicketing.Ticketing.Api.Endpoints;

public static class SubscriptionEndpoints
{
    public static void MapSubscriptionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/subscriptions").WithTags("Subscriptions");

        // Buyers, not organizers: a subscription belongs to the person who bought it, so these are
        // plain RequireAuthorization() with an ownership check inside the service — the same shape
        // GET /tickets/mine uses.
        group.MapGet("/mine", GetMine).RequireAuthorization().WithValidation<SubscriptionQuery>();
        group.MapPost("/{id:guid}/cancel", Cancel).RequireAuthorization();
    }

    private static async Task<IResult> GetMine(
        [AsParameters] SubscriptionQuery query, ISubscriptionService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetMineAsync(query, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Cancel(
        Guid id, ISubscriptionService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.CancelAsync(id, http.User, ct);
        return result.ToHttpResult();
    }
}
