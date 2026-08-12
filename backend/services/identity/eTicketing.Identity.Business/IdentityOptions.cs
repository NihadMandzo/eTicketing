namespace eTicketing.Identity.Business;

/// <summary>
/// Bound from the "Identity" configuration section. <see cref="PublicBaseUrl"/> must be the
/// Gateway's externally-reachable address (e.g. "http://localhost:5000/api"), NOT
/// identity-service's internal docker-network name — identity-service has no published port,
/// only the Gateway is reachable from the Flutter clients. Used to build absolute organization
/// logo URLs (OrganizationService.BuildLogoUrl), same reasoning as Catalog's CatalogOptions.
/// </summary>
public class IdentityOptions
{
    public const string SectionName = "Identity";

    public string PublicBaseUrl { get; set; } = "http://localhost:5000/api";
}
