namespace eTicketing.Ticketing.Business.External;

/// <summary>Mirrors eTicketing.Catalog.Business.Products.OrganizationProductStatsResponse's JSON
/// shape — duplicated across the service boundary for the same reason as
/// <see cref="CatalogProductResponse"/>.</summary>
public record CatalogOrganizationProductStats(
    Guid OrganizationId,
    int Total,
    int Published,
    int Draft,
    int WithoutImage);
