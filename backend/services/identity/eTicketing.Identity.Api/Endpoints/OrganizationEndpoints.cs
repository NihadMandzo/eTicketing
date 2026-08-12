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
        group.MapGet("/{id:guid}/logo", GetLogo).AllowAnonymous();

        // multipart/form-data — the desktop app always posts an optional Logo file alongside
        // the text fields (see OrganizationProvider.insertOrganization/updateOrganization).
        group.MapPost("", Create).RequireAuthorization("SuperAdminOnly").WithValidation<CreateOrganizationRequest>().DisableAntiforgery();
        group.MapPut("/{id:guid}", Update).RequireAuthorization("PlatformStaff").WithValidation<UpdateOrganizationRequest>().DisableAntiforgery();
        group.MapDelete("/{id:guid}", Delete).RequireAuthorization("SuperAdminOnly");

        group.MapGet("/{id:guid}/users", GetUsers).RequireAuthorization().WithValidation<OrganizationUserQuery>();
        group.MapPost("/{id:guid}/users", AddUser).RequireAuthorization("SuperAdminOnly").WithValidation<AddOrganizationUserRequest>();
        group.MapDelete("/{id:guid}/users/{userId:guid}", RemoveUser).RequireAuthorization("SuperAdminOnly");
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

    private static async Task<IResult> Create([FromForm] CreateOrganizationRequest request, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> Update(Guid id, [FromForm] UpdateOrganizationRequest request, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, request, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetLogo(Guid id, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.GetLogoAsync(id, ct);
        return result.IsSuccess ? Results.File(result.Value!.Data, result.Value!.ContentType) : result.ToHttpResult();
    }

    private static async Task<IResult> Delete(Guid id, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> GetUsers(Guid id, [AsParameters] OrganizationUserQuery query, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.GetUsersAsync(id, query, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> AddUser(Guid id, AddOrganizationUserRequest request, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.AddUserAsync(id, request, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> RemoveUser(Guid id, Guid userId, IOrganizationService service, CancellationToken ct)
    {
        var result = await service.RemoveUserAsync(id, userId, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }
}
