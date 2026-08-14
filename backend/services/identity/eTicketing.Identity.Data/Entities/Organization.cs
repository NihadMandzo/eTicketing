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

    // Logo bytes live in Azure Blob Storage ("organization-logos" container), not in IdentityDb —
    // only the blob's key is persisted here, so it can be looked up for delete/replace without
    // re-deriving it from the (mutable) Name. Null until OrganizationService.UploadLogoAsync is
    // called; LogoUrl on OrganizationResponse is derived from this via IBlobStorageService, not
    // stored. See eTicketing.Shared.Storage.BlobNaming for the naming convention.
    public string? LogoBlobName { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
}
