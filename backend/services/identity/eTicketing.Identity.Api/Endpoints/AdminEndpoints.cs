using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Admins;

namespace eTicketing.Identity.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admins").WithTags("Admins").RequireAuthorization("SuperAdminOnly");

        group.MapGet("", GetAll);
        group.MapGet("/{id:int}", GetById);
        group.MapPost("", Create);
        group.MapDelete("/{id:int}", Delete);
    }

    private static async Task<IResult> GetAll([AsParameters] AdminQuery query, IAdminService service, CancellationToken ct)
    {
        var result = await service.GetAsync(query, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetById(int id, IAdminService service, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Create(CreateAdminRequest request, IAdminService service, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> Delete(int id, IAdminService service, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }
}
