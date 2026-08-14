using eTicketing.Catalog.Business.Categories;
using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using Microsoft.AspNetCore.Mvc;

namespace eTicketing.Catalog.Api.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/categories").WithTags("Categories");

        group.MapGet("", GetAll).AllowAnonymous().WithValidation<CategoryQuery>();
        group.MapGet("/{id:int}", GetById).AllowAnonymous();

        group.MapPost("", Create).RequireAuthorization("PlatformStaff").WithValidation<CreateCategoryRequest>();
        group.MapPut("/{id:int}", Update).RequireAuthorization("PlatformStaff").WithValidation<UpdateCategoryRequest>();
        group.MapDelete("/{id:int}", Delete).RequireAuthorization("PlatformStaff");

        // Icons are managed exclusively through these two dedicated multipart endpoints, never
        // bundled into Create/Update above — see Category.IconBlobName / CategoryService.
        group.MapPost("/{id:int}/icon", UploadIcon).RequireAuthorization("PlatformStaff")
            .WithValidation<CategoryIconUploadRequest>().DisableAntiforgery();
        group.MapPut("/{id:int}/icon", ReplaceIcon).RequireAuthorization("PlatformStaff")
            .WithValidation<CategoryIconUploadRequest>().DisableAntiforgery();
    }

    private static async Task<IResult> GetAll([AsParameters] CategoryQuery query, ICategoryService service, CancellationToken ct)
    {
        var result = await service.GetAsync(query, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetById(int id, ICategoryService service, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Create(CreateCategoryRequest request, ICategoryService service, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> Update(int id, UpdateCategoryRequest request, ICategoryService service, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, request, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Delete(int id, ICategoryService service, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> UploadIcon(int id, [FromForm] CategoryIconUploadRequest request, ICategoryService service, CancellationToken ct)
    {
        var result = await service.UploadIconAsync(id, request.Icon, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> ReplaceIcon(int id, [FromForm] CategoryIconUploadRequest request, ICategoryService service, CancellationToken ct)
    {
        var result = await service.ReplaceIconAsync(id, request.Icon, ct);
        return result.ToHttpResult();
    }
}
