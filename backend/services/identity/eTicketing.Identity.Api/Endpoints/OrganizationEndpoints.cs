using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Identity.Business.Organizations;
using Microsoft.AspNetCore.Mvc;

namespace eTicketing.Identity.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/organizations").WithTags("Organizations");

        group.MapGet("", GetAll).AllowAnonymous().WithValidation<OrganizationQuery>();
        group.MapGet("/{id:guid}", GetById).AllowAnonymous();

        group.MapPost("", Create).RequireAuthorization("SuperAdminOnly").WithValidation<CreateOrganizationRequest>();
        group.MapPut("/{id:guid}", Update).RequireAuthorization("PlatformStaff").WithValidation<UpdateOrganizationRequest>();
        group.MapDelete("/{id:guid}", Delete).RequireAuthorization("SuperAdminOnly");

        // Logos are managed exclusively through these two dedicated multipart endpoints, never
        // bundled into Create/Update above — see Organization.LogoBlobName / OrganizationService.
        group.MapPost("/{id:guid}/logo", UploadLogo).RequireAuthorization("PlatformStaff")
            .WithValidation<OrganizationLogoUploadRequest>().DisableAntiforgery();
        group.MapPut("/{id:guid}/logo", ReplaceLogo).RequireAuthorization("PlatformStaff")
            .WithValidation<OrganizationLogoUploadRequest>().DisableAntiforgery();

        // Internal only — mapped on `app`, not on the /organizations group, so it never picks up
        // that group's public prefix or any auth the group later gains. Same convention as
        // Catalog's /internal/products/* routes: no Gateway route points here, so it is reachable
        // only from inside the compose network (see .claude/rules/01-domain.md).
        app.MapPost("/internal/organizations/by-ids", GetInternalByIds).WithTags("Organizations (internal)");

        group.MapGet("/{id:guid}/users", GetUsers).RequireAuthorization("Organizer").WithValidation<OrganizationUserQuery>();
        group.MapPost("/{id:guid}/users", AddUser).RequireAuthorization("Organizer").WithValidation<AddOrganizationUserRequest>();
        group.MapPut("/{id:guid}/users/{userId:guid}", UpdateUser).RequireAuthorization("Organizer").WithValidation<UpdateOrganizationUserRequest>();
        group.MapDelete("/{id:guid}/users/{userId:guid}", RemoveUser).RequireAuthorization("Organizer");
    }

    private static async Task<IResult> GetAll([AsParameters] OrganizationQuery query, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.GetAsync(query, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetById(Guid id, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetInternalByIds(List<Guid> ids, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.GetInternalByIdsAsync(ids, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Create(CreateOrganizationRequest request, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> Update(Guid id, UpdateOrganizationRequest request, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, request, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Delete(Guid id, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> UploadLogo(Guid id, [FromForm] OrganizationLogoUploadRequest request, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.UploadLogoAsync(id, request.Logo, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> ReplaceLogo(Guid id, [FromForm] OrganizationLogoUploadRequest request, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.ReplaceLogoAsync(id, request.Logo, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetUsers(Guid id, [AsParameters] OrganizationUserQuery query, IOrganizationService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetUsersAsync(id, query, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> AddUser(Guid id, AddOrganizationUserRequest request, IOrganizationService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.AddUserAsync(id, request, http.User, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateUser(Guid id, Guid userId, UpdateOrganizationUserRequest request, IOrganizationService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.UpdateUserAsync(id, userId, request, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> RemoveUser(Guid id, Guid userId, IOrganizationService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.RemoveUserAsync(id, userId, http.User, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }
}
