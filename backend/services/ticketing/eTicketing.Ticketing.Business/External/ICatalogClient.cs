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

    /// <summary>Catalogue-side counts per organization, for the Organizacije tab of GET /reports.
    /// Ticketing knows how many tickets an organization sold but nothing about how many products
    /// it has, how many are still drafts, or which are missing a photo — those are Catalog's to
    /// answer. Returns a row for every organization that owns at least one product; an
    /// organization with none is simply absent.</summary>
    Task<IReadOnlyList<CatalogOrganizationProductStats>> GetOrganizationProductStatsAsync(CancellationToken ct = default);

    /// <summary>Published, SingleOccurrence products with a future date, soonest first — for the
    /// Dashboard's "Nadolazeći događaji" card. Product identity/date lives in Catalog; Ticketing
    /// adds capacity/sold once it has the candidate ids back (see ReportService.GetUpcomingEventsAsync).
    /// organizationId null means platform-wide (SuperAdmin/Admin).</summary>
    Task<IReadOnlyList<CatalogProductResponse>> GetUpcomingProductsAsync(
        Guid? organizationId, int count, CancellationToken ct = default);
}

/// <summary>Mirrors eTicketing.Catalog.Business.Products.OrganizationProductStatsResponse's JSON
/// shape — duplicated across the service boundary for the same reason as
/// <see cref="CatalogProductResponse"/>.</summary>
public record CatalogOrganizationProductStats(
    Guid OrganizationId,
    int Total,
    int Published,
    int Draft,
    int WithoutImage);
