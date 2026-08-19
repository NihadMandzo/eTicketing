using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Entities;

/// <summary>
/// One gallery photo belonging to a Product (up to 5, enforced in ProductService.UploadImageAsync).
/// Bytes live in Azure Blob Storage ("product-images" container), not in CatalogDb — only the
/// blob's key is persisted here, mirroring Category.IconBlobName / Organization.LogoBlobName. The
/// difference from those two is cardinality: a Product can have several images, so each gets its
/// own row/blob key instead of a single nullable column on Product itself.
/// </summary>
public class ProductImage : BaseEntity
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    // See eTicketing.Shared.Storage.BlobNaming — built from this image's own Id (not the
    // product's), so multiple images on the same product never collide on the same key.
    public string BlobName { get; set; } = string.Empty;

    // Upload order — first uploaded is the "cover" image (DisplayOrder 0), shown first in the
    // gallery. Assigned once at upload time, never reordered (no drag-to-reorder UI exists yet).
    public int DisplayOrder { get; set; }
}
