using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Business.Products;

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
    string? ImageUrl,
    DateTime CreatedAt);

/// <summary>Returned by both POST /products/preview (stateless, no DB write) and as the shape
/// PreviewAsync/CreateAsync validate against — see ProductService.Validate().</summary>
public record ProductPreviewResponse(
    string Name,
    string Description,
    DateTime? Date,
    int CategoryId,
    string CategoryName,
    TicketingMode TicketingMode,
    string? ImageUrl);

/// <summary>Same shape for create and update — OrganizationId is never taken from this request,
/// always from the caller's JWT claim (see ProductService.CreateAsync).</summary>
public record UpsertProductRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime? Date { get; init; }
    public int CategoryId { get; init; }
    public string? ImageUrl { get; init; }
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
