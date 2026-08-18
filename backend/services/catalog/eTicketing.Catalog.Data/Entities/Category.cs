using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Entities;

public class Category : BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    // Governs how every Product in this category (and, in eTicketing.Ticketing, every Sector of
    // those products) behaves — see TicketingMode's doc comments. Defaults to the classic
    // one-time-event shape so existing/seeded categories keep working unchanged.
    public TicketingMode TicketingMode { get; set; } = TicketingMode.SingleOccurrence;

    // Icon bytes live in Azure Blob Storage ("category-icons" container), not in CatalogDb —
    // only the blob's key is persisted here, so it can be looked up for delete/replace without
    // re-deriving it from the (mutable) Name. Null until CategoryService.UploadIconAsync is
    // called; IconUrl on CategoryResponse is derived from this via IBlobStorageService, not
    // stored. See eTicketing.Shared.Storage.BlobNaming for the naming convention.
    public string? IconBlobName { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
