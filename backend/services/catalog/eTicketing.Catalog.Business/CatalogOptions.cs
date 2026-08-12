namespace eTicketing.Catalog.Business;

/// <summary>
/// Bound from the "Catalog" configuration section. <see cref="PublicBaseUrl"/> must be the
/// Gateway's externally-reachable address (e.g. "http://localhost:5000/api"), NOT
/// catalog-service's internal docker-network name — catalog-service has no published port,
/// only the Gateway is reachable from the Flutter clients. Used to build absolute icon URLs
/// (CategoryService.BuildIconUrl) since the desktop app calls Image.network(iconUrl) directly
/// with no base-URL prefixing of its own.
/// </summary>
public class CatalogOptions
{
    public const string SectionName = "Catalog";

    public string PublicBaseUrl { get; set; } = "http://localhost:5000/api";
}
