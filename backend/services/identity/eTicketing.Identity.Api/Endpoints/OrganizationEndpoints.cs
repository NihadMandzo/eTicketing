using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Organizations;
using eTicketing.Identity.Business.Security;

namespace eTicketing.Identity.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/organizations").WithTags("Organizations");

        group.MapGet("", GetAll).AllowAnonymous();
        group.MapGet("/{id:int}", GetById).AllowAnonymous();

        group.MapPost("", Create).RequireAuthorization("SuperAdminOnly");
        group.MapPut("/{id:int}", Update).RequireAuthorization("PlatformStaff");
        group.MapDelete("/{id:int}", Delete).RequireAuthorization("SuperAdminOnly");

        group.MapGet("/{id:int}/users", GetUsers).RequireAuthorization();
        group.MapPost("/{id:int}/users", AddUser).RequireAuthorization("SuperAdminOnly");
        group.MapDelete("/{id:int}/users/{userId:int}", RemoveUser).RequireAuthorization("SuperAdminOnly");
    }

    private static async Task<IResult> GetAll([AsParameters] OrganizationQuery query, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.GetAsync(query, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetById(int id, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Create(CreateOrganizationRequest request, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> Update(int id, UpdateOrganizationRequest request, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, request, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Delete(int id, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> GetUsers(int id, [AsParameters] BaseSearchObject query, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.GetUsersAsync(id, query, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> AddUser(int id, AddOrganizationUserRequest request, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.AddUserAsync(id, request, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> RemoveUser(int id, int userId, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.RemoveUserAsync(id, userId, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }
}
