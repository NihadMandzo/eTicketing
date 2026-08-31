using eTicketing.Catalog.Business.Products;
using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using Microsoft.AspNetCore.Mvc;

namespace eTicketing.Catalog.Api.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/products").WithTags("Products");

        group.MapGet("", GetPublished).AllowAnonymous().WithValidation<ProductQuery>();
        group.MapGet("/{id:guid}", GetById).AllowAnonymous();
        group.MapGet("/mine", GetMine).RequireAuthorization("Organizer").WithValidation<ProductQuery>();
        group.MapGet("/all", GetAll).RequireAuthorization("PlatformStaff").WithValidation<ProductQuery>();
        group.MapGet("/organization-ids", GetOrganizationIds).RequireAuthorization("PlatformStaff");

        group.MapPost("/preview", Preview).RequireAuthorization("Organizer").WithValidation<UpsertProductRequest>();
        group.MapPost("", Create).RequireAuthorization("Organizer").WithValidation<UpsertProductRequest>();
        group.MapPost("/{id:guid}/publish", Publish).RequireAuthorization("Organizer");
        group.MapPut("/{id:guid}", Update).RequireAuthorization("Organizer").WithValidation<UpsertProductRequest>();
        group.MapDelete("/{id:guid}", Delete).RequireAuthorization("Organizer");

        // Images are managed exclusively through these two dedicated multipart endpoints, never
        // bundled into Create/Update above — see Product.Images / ProductService.
        group.MapPost("/{id:guid}/images", UploadImage).RequireAuthorization("Organizer")
            .WithValidation<ProductImageUploadRequest>().DisableAntiforgery();
        group.MapDelete("/{id:guid}/images/{imageId:guid}", DeleteImage).RequireAuthorization("Organizer");

        // Not routed through the Gateway — see docs/gateway-tok.md §1. Consumed only by
        // eTicketing.Ticketing's internal Catalog client.
        app.MapGet("/internal/products/{id:guid}", GetInternal).WithTags("Products (internal)");
        // POST for a read on purpose: the id list is long enough (an organizer can be validating
        // tickets for dozens of products in one day) that it would not comfortably fit in a query
        // string. Long, but not unbounded — ProductService caps it, see MaxInternalByIdsCount.
        app.MapPost("/internal/products/by-ids", GetInternalByIds).WithTags("Products (internal)");
        app.MapGet("/internal/products/organization-stats", GetOrganizationStats).WithTags("Products (internal)");
    }

    private static async Task<IResult> GetPublished([AsParameters] ProductQuery query, IProductService service, CancellationToken ct)
    {
        var result = await service.GetPublishedAsync(query, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetById(Guid id, IProductService service, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetMine([AsParameters] ProductQuery query, IProductService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetMineAsync(query, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetAll([AsParameters] ProductQuery query, IProductService service, CancellationToken ct)
    {
        var result = await service.GetAllAsync(query, ct);
        return result.ToHttpResult();
    }

    /// <summary>categoryIds is a single comma-joined query param ("?categoryIds=1,2,3"),
    /// deliberately not ASP.NET Core's repeated-key array binding — matches how the desktop
    /// client builds this call.</summary>
    private static async Task<IResult> GetOrganizationIds(string? categoryIds, IProductService service, CancellationToken ct)
    {
        var ids = (categoryIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var v) ? v : (int?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0) return Results.Ok(new List<Guid>());

        var result = await service.GetOrganizationIdsAsync(ids, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Preview(UpsertProductRequest request, IProductService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.PreviewAsync(request, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Create(UpsertProductRequest request, IProductService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, http.User, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> Publish(Guid id, IProductService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.PublishAsync(id, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Update(Guid id, UpsertProductRequest request, IProductService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, request, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Delete(Guid id, IProductService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, http.User, ct);
        return result.ToHttpResult(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> GetInternal(Guid id, IProductService service, CancellationToken ct)
    {
        var result = await service.GetInternalAsync(id, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetOrganizationStats(IProductService service, CancellationToken ct)
    {
        var result = await service.GetOrganizationStatsAsync(ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetInternalByIds(List<Guid> ids, IProductService service, CancellationToken ct)
    {
        var result = await service.GetInternalByIdsAsync(ids, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> UploadImage(Guid id, [FromForm] ProductImageUploadRequest request, IProductService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.UploadImageAsync(id, request.Image, http.User, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> DeleteImage(Guid id, Guid imageId, IProductService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.DeleteImageAsync(id, imageId, http.User, ct);
        return result.ToHttpResult();
    }
}
