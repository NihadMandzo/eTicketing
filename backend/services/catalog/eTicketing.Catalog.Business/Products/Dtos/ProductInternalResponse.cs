using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Products;

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
