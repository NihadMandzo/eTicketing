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
    double Latitude,
    double Longitude,
    City City,
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
    TicketingMode TicketingMode,
    double Latitude,
    double Longitude,
    City City);

/// <summary>Same shape for create and update — OrganizationId is never taken from this request,
/// always from the caller's JWT claim (see ProductService.CreateAsync). Images are managed
/// exclusively through the dedicated POST/DELETE /products/{id}/images endpoints, never bundled
/// in here — same convention as Category's icon. Latitude/Longitude/City are nullable here (so
/// "not sent" is distinguishable from "0,0"/"first enum value") but required in practice — see
/// CreateProductRequestValidator: the organizer must place an exact pin at creation time.</summary>
public record UpsertProductRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime? Date { get; init; }
    public int CategoryId { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public City? City { get; init; }
}

public sealed record ProductQuery : BaseSearchObject
{
    public Guid? OrganizationId { get; init; }
    public int? CategoryId { get; init; }
    public PublishStatus? Status { get; init; }

    // Optional — narrows results to a single city (see Product.City), unlike CategoryId/Status
    // this filter is available on the public GET /products endpoint too.
    public City? City { get; init; }
}

/// <summary>Internal-only shape returned by GET /internal/products/{id} and
/// POST /internal/products/by-ids — never routed through the Gateway. Consumed by
/// eTicketing.Ticketing to verify Sector-creation ownership, to copy the product's TicketingMode
/// onto a new Sector, and (Name/Date) to label the organizer's gate-validation list and decide
/// whether a SingleOccurrence ticket is valid today. Kept mirrored by
/// eTicketing.Ticketing.Business.External.CatalogProductResponse — property ORDER matters there,
/// it deserializes this by name but the record is positional on both sides.</summary>
public record ProductInternalResponse(
    Guid Id,
    Guid OrganizationId,
    PublishStatus Status,
    TicketingMode TicketingMode,
    string Name,
    DateTime? Date,
    City City);

/// <summary>Catalogue-side counts for one organization, returned by the internal
/// GET /internal/products/organization-stats that eTicketing.Ticketing's Organizacije report
/// calls. Internal-only, like ProductInternalResponse above: this is deliberately not exposed
/// through the Gateway, because "how many drafts does that organization have" is not something a
/// public caller should be able to enumerate.</summary>
public record OrganizationProductStatsResponse(
    Guid OrganizationId,
    int Total,
    int Published,
    int Draft,
    int WithoutImage);

/// <summary>Plain mutable class, not a record — carries an IFormFile, bound via [FromForm].
/// Shape for POST /products/{id}/images (there is no PUT/replace equivalent — a full product can
/// carry up to 5 images, so "replace" is just "delete one, upload another").</summary>
public class ProductImageUploadRequest
{
    public IFormFile Image { get; set; } = null!;
}
