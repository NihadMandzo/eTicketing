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

/// <summary>HTTP client interface to eTicketing.Catalog's internal-only endpoints (never routed
/// through the Gateway). Used at Sector-creation time to verify the caller's organization owns the
/// Product and to copy its Category.TicketingMode onto the new Sector, and at gate-validation time
/// to resolve product names/dates for the organizer's "today" list. Interface lives in .Business
/// per .claude/rules/10-backend.md; the concrete HttpClient-backed implementation lives in
/// .Api/Infrastructure (HTTP wiring is a hosting concern).</summary>
public interface ICatalogClient
{
    Task<CatalogProductResponse?> GetProductAsync(Guid productId, CancellationToken ct = default);

    /// <summary>Batch form of <see cref="GetProductAsync"/>. The organizer's validation list needs
    /// the name and date of every product they have live tickets for today — one call, not one per
    /// product. Unknown ids are simply absent from the result, never an error.</summary>
    Task<IReadOnlyList<CatalogProductResponse>> GetProductsAsync(IReadOnlyList<Guid> productIds, CancellationToken ct = default);
}
