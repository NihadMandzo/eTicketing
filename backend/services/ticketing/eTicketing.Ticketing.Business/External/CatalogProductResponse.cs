using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Business.External;

/// <summary>Mirrors eTicketing.Catalog.Business.Products.ProductInternalResponse's JSON shape —
/// duplicated rather than shared across the service boundary (Ticketing never references
/// Catalog's assemblies, only its HTTP contract), same reasoning as every other cross-service
/// call in this codebase.</summary>
public record CatalogProductResponse(
    Guid Id,
    Guid OrganizationId,
    PublishStatus Status,
    TicketingMode TicketingMode,
    string Name,
    DateTime? Date,
    City City);
