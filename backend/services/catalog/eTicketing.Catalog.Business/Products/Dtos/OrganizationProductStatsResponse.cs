namespace eTicketing.Catalog.Business.Products;

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
