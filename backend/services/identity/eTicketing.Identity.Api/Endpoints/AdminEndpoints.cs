using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Identity.Business.Admins;
using Microsoft.AspNetCore.Mvc;

namespace eTicketing.Identity.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admins").WithTags("Admins").RequireAuthorization("SuperAdminOnly");

        group.MapGet("", GetAll).WithValidation<AdminQuery>();
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("", Create).WithValidation<CreateAdminRequest>();
        group.MapPut("/{id:guid}", Update).WithValidation<UpdateStaffUserRequest>();
        group.MapDelete("/{id:guid}", Delete).WithValidation<DeleteAdminRequest>();
        group.MapPost("/{id:guid}/set-password", SetPassword).WithValidation<SetPasswordRequest>();
    }

    private static async Task<IResult> GetAll([AsParameters] AdminQuery query, IAdminService service, CancellationToken ct)
    {
        var result = await service.GetAsync(query, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetById(Guid id, IAdminService service, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Create(CreateAdminRequest request, IAdminService service, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> Update(Guid id, UpdateStaffUserRequest request, IAdminService service, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, request, ct);
        return result.ToHttpResult();
    }

    // Body is now required on this DELETE (Reason/RecipientEmail — see DeleteAdminRequest) —
    // every caller must always send JSON, even `{}` when deleting a non-OrganizationAdmin
    // target, since a bare bodyless DELETE no longer binds here. [FromBody] is mandatory here:
    // Minimal APIs refuse to *infer* a body parameter on MapDelete/MapGet/MapHead (by design,
    // since those verbs conventionally carry no body) and throw at startup
    // ("Body was inferred but the method does not allow inferred body parameters") without it.
    private static async Task<IResult> Delete(Guid id, [FromBody] DeleteAdminRequest request, IAdminService service, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, request, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> SetPassword(Guid id, SetPasswordRequest request, IAdminService service, CancellationToken ct)
    {
        var result = await service.SetPasswordAsync(id, request, ct);
        return result.ToHttpResult();
    }
}
