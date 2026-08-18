using eTicketing.Contracts.Persistence;

namespace eTicketing.Ticketing.Business.External;

/// <summary>Mirrors eTicketing.Catalog.Business.Products.ProductInternalResponse's JSON shape —
/// duplicated rather than shared across the service boundary (Ticketing never references
/// Catalog's assemblies, only its HTTP contract), same reasoning as every other cross-service
/// call in this codebase.</summary>
public record CatalogProductResponse(Guid Id, Guid OrganizationId, PublishStatus Status, TicketingMode TicketingMode);

/// <summary>HTTP client interface to eTicketing.Catalog's internal-only endpoint
/// (GET /internal/products/{id}, never routed through the Gateway) — used at Sector-creation time
/// to verify the caller's organization owns the Product and to copy its Category.TicketingMode
/// onto the new Sector. Interface lives in .Business per .claude/rules/10-backend.md; the
/// concrete HttpClient-backed implementation lives in .Api/Infrastructure (HTTP wiring is a
/// hosting concern).</summary>
public interface ICatalogClient
{
    Task<CatalogProductResponse?> GetProductAsync(Guid productId, CancellationToken ct = default);
}
