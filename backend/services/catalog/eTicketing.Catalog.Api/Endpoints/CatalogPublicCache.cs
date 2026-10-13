namespace eTicketing.Catalog.Api.Endpoints;

/// <summary>
/// One tag for every cached storefront read in this service — products, categories and the
/// anonymous recommendation rows all show product data, so any catalog write evicts them all.
/// 30 seconds: a new or edited product shows up at once on the replica that took the write, and on
/// the others within half a minute.
/// </summary>
internal static class CatalogPublicCache
{
    public const string Tag = "catalog-public";

    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);
}
