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
        group.MapGet("/{id:int}/icon", GetIcon).AllowAnonymous();

        group.MapPost("", Create).RequireAuthorization("PlatformStaff").WithValidation<CreateCategoryRequest>().DisableAntiforgery();
        group.MapPut("/{id:int}", Update).RequireAuthorization("PlatformStaff").WithValidation<UpdateCategoryRequest>().DisableAntiforgery();
        group.MapDelete("/{id:int}", Delete).RequireAuthorization("PlatformStaff");
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

    private static async Task<IResult> GetIcon(int id, ICategoryService service, CancellationToken ct)
    {
        var result = await service.GetIconAsync(id, ct);
        return result.IsSuccess ? Results.File(result.Value!.Data, result.Value!.ContentType) : result.ToHttpResult();
    }

    private static async Task<IResult> Create([FromForm] CreateCategoryRequest request, ICategoryService service, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> Update(int id, [FromForm] UpdateCategoryRequest request, ICategoryService service, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, request, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Delete(int id, ICategoryService service, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }
}
