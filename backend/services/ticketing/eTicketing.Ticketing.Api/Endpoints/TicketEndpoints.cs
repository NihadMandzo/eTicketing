using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Ticketing.Business.Tickets;

namespace eTicketing.Ticketing.Api.Endpoints;

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/tickets").WithTags("Tickets");

        // Deliberately only /mine for the buyer side this pass — SuperAdmin/Admin override
        // (/tickets/all, PUT, DELETE) stays deferred, see .claude/rules/01-domain.md.
        group.MapGet("/mine", GetMine).RequireAuthorization().WithValidation<TicketQuery>();

        // Gate validation. "Organizer" also admits Admin/SuperAdmin (see
        // AuthorizationPolicyExtensions), which is the PlatformStaff override — the service then
        // skips the per-organization ownership check for them.
        group.MapGet("/validation/products", GetValidationProducts).RequireAuthorization("Organizer");
        group.MapPost("/validate", Validate).RequireAuthorization("Organizer").WithValidation<ValidateTicketRequest>();
    }

    private static async Task<IResult> GetMine([AsParameters] TicketQuery query, ITicketService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetMineAsync(query, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetValidationProducts(ITicketValidationService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetProductsForTodayAsync(http.User, ct);
        return result.ToHttpResult();
    }

    /// <summary>A scanned ticket that turns out to be invalid still comes back 200 with
    /// IsValid=false — the scanner needs the reason to paint a red card, see
    /// TicketValidationResponse. Only 403 (not an organizer) and 409 (another device is validating
    /// this same ticket right now) are real error statuses here.</summary>
    private static async Task<IResult> Validate(ValidateTicketRequest request, ITicketValidationService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.ValidateAsync(request, http.User, ct);
        return result.ToHttpResult();
    }
}
