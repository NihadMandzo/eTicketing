using eTicketing.Catalog.Data.Entities;
using eTicketing.Shared.Storage;
using Mapster;

namespace eTicketing.Catalog.Business.Products.Mapping;

/// <summary>
/// The single Product → ProductResponse mapping, shared by ProductService and
/// RecommendationService. Extracted from ProductService (where it lived as a private method) rather
/// than duplicated when recommendations needed it too: image URLs are derived from Azure Blob
/// Storage instead of stored as columns, so a second copy of this logic would be a second place for
/// that derivation to drift.
/// </summary>
public sealed class ProductResponseFactory
{
    /// <summary>Blob container holding every product's gallery photos. Public (anonymously
    /// readable) by design — these URLs go straight into an &lt;img src&gt;.</summary>
    public const string ContainerName = "product-images";

    private readonly IBlobStorageService _blobStorageService;

    public ProductResponseFactory(IBlobStorageService blobStorageService)
    {
        _blobStorageService = blobStorageService;
    }

    /// <summary>Caller must have loaded Category and Images — this maps what is there, it does not
    /// go back to the database. A product materialized without .Include(p =&gt; p.Images) maps to an
    /// empty gallery, not to an exception.</summary>
    public ProductResponse ToResponse(Product product) =>
        product.Adapt<ProductResponse>() with { Images = BuildImages(product) };

    public List<ProductResponse> ToResponses(IEnumerable<Product> products) =>
        products.Select(ToResponse).ToList();

    private List<ProductImageResponse> BuildImages(Product product) =>
        product.Images
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new ProductImageResponse(i.Id, _blobStorageService.GetPublicUrl(ContainerName, i.BlobName), i.DisplayOrder))
            .ToList();
}
