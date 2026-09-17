using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Ticketing.Business.Sectors;

namespace eTicketing.Ticketing.Api.Endpoints;

public static class SectorEndpoints
{
    public static void MapSectorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/sectors").WithTags("Sectors");

        group.MapGet("", GetPublished).AllowAnonymous().WithValidation<SectorQuery>();
        group.MapGet("/mine", GetMine).RequireAuthorization("Organizer").WithValidation<SectorQuery>();
        group.MapGet("/all", GetAll).RequireAuthorization("PlatformStaff").WithValidation<SectorQuery>();

        group.MapPost("/preview", Preview).RequireAuthorization("Organizer").WithValidation<UpsertSectorRequest>();
        group.MapPost("", Create).RequireAuthorization("Organizer").WithValidation<UpsertSectorRequest>();
        group.MapPost("/{id:guid}/publish", Publish).RequireAuthorization("Organizer");
        group.MapPut("/{id:guid}", Update).RequireAuthorization("Organizer").WithValidation<UpsertSectorRequest>();
        group.MapDelete("/{id:guid}", Delete).RequireAuthorization("Organizer");

        group.MapPost("/{id:guid}/hold", Hold).RequireAuthorization().WithValidation<HoldSectorRequest>();
        group.MapPost("/holds/{holdId}/release", ReleaseHold).RequireAuthorization();
    }

    private static async Task<IResult> GetPublished([AsParameters] SectorQuery query, ISectorService service, CancellationToken ct)
    {
        var result = await service.GetPublishedAsync(query, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetMine([AsParameters] SectorQuery query, ISectorService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetMineAsync(query, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetAll([AsParameters] SectorQuery query, ISectorService service, CancellationToken ct)
    {
        var result = await service.GetAllAsync(query, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Preview(UpsertSectorRequest request, ISectorService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.PreviewAsync(request, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Create(UpsertSectorRequest request, ISectorService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, http.User, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> Publish(Guid id, ISectorService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.PublishAsync(id, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Update(Guid id, UpsertSectorRequest request, ISectorService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, request, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Delete(Guid id, ISectorService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, http.User, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> Hold(Guid id, HoldSectorRequest request, ISectorService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.HoldAsync(id, request, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ReleaseHold(string holdId, ISectorService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.ReleaseHoldAsync(holdId, http.User, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }
}
