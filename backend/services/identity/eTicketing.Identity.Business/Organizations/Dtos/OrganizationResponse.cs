namespace eTicketing.Identity.Business.Organizations;

/// <summary>LogoUrl is null until a logo has been uploaded via the dedicated
/// POST/PUT /organizations/{id}/logo endpoints — organizations no longer carry logo bytes at
/// all (see Organization.LogoBlobName); it's derived from Azure Blob Storage, not a stored
/// column.</summary>
public record OrganizationResponse(
    Guid Id,
    string Name,
    string Description,
    string Address,
    string PhoneNumber,
    string Email,
    string? Website,
    string? LogoUrl,
    bool IsActive,
    int UserCount,
    DateTime CreatedAt);
