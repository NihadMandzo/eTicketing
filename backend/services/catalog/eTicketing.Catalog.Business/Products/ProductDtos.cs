using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Products;

/// <summary>One uploaded gallery photo, URL derived from Azure Blob Storage — never a stored
/// column, same reasoning as CategoryResponse.IconUrl.</summary>
public record ProductImageResponse(Guid Id, string Url, int DisplayOrder);

public record ProductResponse(
    Guid Id,
    string Name,
    string Description,
    DateTime? Date,
    int CategoryId,
    string CategoryName,
    TicketingMode TicketingMode,
    Guid OrganizationId,
    PublishStatus Status,
    IReadOnlyList<ProductImageResponse> Images,
    DateTime CreatedAt);

/// <summary>Returned by both POST /products/preview (stateless, no DB write) and as the shape
/// PreviewAsync/CreateAsync validate against — see ProductService.Validate(). Images are
/// deliberately absent: a previewed product has no Id yet to attach an uploaded image to (same
/// reasoning as Category, whose icon can only be uploaded once the category exists).</summary>
public record ProductPreviewResponse(
    string Name,
    string Description,
    DateTime? Date,
    int CategoryId,
    string CategoryName,
    TicketingMode TicketingMode);

/// <summary>Same shape for create and update — OrganizationId is never taken from this request,
/// always from the caller's JWT claim (see ProductService.CreateAsync). Images are managed
/// exclusively through the dedicated POST/DELETE /products/{id}/images endpoints, never bundled
/// in here — same convention as Category's icon.</summary>
public record UpsertProductRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime? Date { get; init; }
    public int CategoryId { get; init; }
}

public sealed record ProductQuery : BaseSearchObject
{
    public Guid? OrganizationId { get; init; }
    public int? CategoryId { get; init; }
    public PublishStatus? Status { get; init; }
}

/// <summary>Internal-only shape returned by GET /internal/products/{id} — never routed through
/// the Gateway, consumed only by eTicketing.Ticketing to verify Sector-creation ownership and to
/// copy the product's TicketingMode onto the new Sector.</summary>
public record ProductInternalResponse(Guid Id, Guid OrganizationId, PublishStatus Status, TicketingMode TicketingMode);

/// <summary>Plain mutable class, not a record — carries an IFormFile, bound via [FromForm].
/// Shape for POST /products/{id}/images (there is no PUT/replace equivalent — a full product can
/// carry up to 5 images, so "replace" is just "delete one, upload another").</summary>
public class ProductImageUploadRequest
{
    public IFormFile Image { get; set; } = null!;
}
