using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Entities;

public class Category : BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    // Icon stored directly in CatalogDb rather than blob storage — no blob storage
    // infrastructure exists anywhere in this repo, and icons are capped small (see
    // Categories/Validators/CreateCategoryRequestValidator.cs), so storing the raw bytes here
    // is simpler than standing up Azurite/blob storage for a ~100x100px image.
    public byte[] IconData { get; set; } = [];
    public string IconContentType { get; set; } = string.Empty; // "image/png" — PNG-only, see CategoryIconValidation

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
