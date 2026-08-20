using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Ticketing.Business.Tickets;

namespace eTicketing.Ticketing.Api.Endpoints;

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/tickets").WithTags("Tickets");

        // Deliberately only /mine this pass — SuperAdmin/Admin override (/tickets/all, PUT, DELETE)
        // stays deferred, see .claude/rules/01-domain.md.
        group.MapGet("/mine", GetMine).RequireAuthorization().WithValidation<TicketQuery>();
    }

    private static async Task<IResult> GetMine([AsParameters] TicketQuery query, ITicketService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetMineAsync(query, http.User, ct);
        return result.ToHttpResult();
    }
}
