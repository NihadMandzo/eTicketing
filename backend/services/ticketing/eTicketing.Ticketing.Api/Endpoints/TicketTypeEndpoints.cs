using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Ticketing.Business.Sectors;

namespace eTicketing.Ticketing.Api.Endpoints;

public static class TicketTypeEndpoints
{
    public static void MapTicketTypeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/sectors/{sectorId:guid}/ticket-types").WithTags("TicketTypes");

        group.MapGet("", GetBySector).AllowAnonymous();
        group.MapPost("", Create).RequireAuthorization("Organizer").WithValidation<UpsertTicketTypeRequest>();
        group.MapPut("/{id:guid}", Update).RequireAuthorization("Organizer").WithValidation<UpsertTicketTypeRequest>();
        group.MapDelete("/{id:guid}", Delete).RequireAuthorization("Organizer");
    }

    private static async Task<IResult> GetBySector(Guid sectorId, ITicketTypeService service, CancellationToken ct)
    {
        var result = await service.GetBySectorAsync(sectorId, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Create(Guid sectorId, UpsertTicketTypeRequest request, ITicketTypeService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.CreateAsync(sectorId, request, http.User, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> Update(Guid sectorId, Guid id, UpsertTicketTypeRequest request, ITicketTypeService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.UpdateAsync(sectorId, id, request, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Delete(Guid sectorId, Guid id, ITicketTypeService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.DeleteAsync(sectorId, id, http.User, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }
}
