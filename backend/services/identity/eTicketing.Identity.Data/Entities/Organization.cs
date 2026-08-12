using eTicketing.Contracts.Persistence;

namespace eTicketing.Identity.Data.Entities;

public class Organization : BaseEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Website { get; set; }
    public bool IsActive { get; set; } = true;

    // Logo stored directly in IdentityDb rather than blob storage — no blob storage
    // infrastructure exists anywhere in this repo (same reasoning as Catalog's Category icon
    // storage). LogoUrl is not a stored column — OrganizationResponse.LogoUrl is computed by
    // OrganizationService from LogoData's presence + IdentityOptions.PublicBaseUrl.
    public byte[]? LogoData { get; set; }
    public string? LogoContentType { get; set; } // "image/png" | "image/jpeg"

    public ICollection<User> Users { get; set; } = new List<User>();
}
