namespace eTicketing.Identity.Business.Organizations;

public record CreateOrganizationRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Website { get; init; }

    /// <summary>Manually typed by SuperAdmin at creation time — the recipient of the
    /// "organization created" notification email. Deliberately independent of AdminEmail: the
    /// account SuperAdmin is standing up (the org's first OrganizationSuperAdmin) is not
    /// necessarily who SuperAdmin wants to notify.</summary>
    public string NotificationEmail { get; init; } = string.Empty;

    // Podaci prvog organizatora — kreira se u istoj transakciji kao i organizacija. Uvijek
    // postaje OrganizationSuperAdmin (vidi OrganizationMappingConfig) — svaka organizacija mora
    // imati tačno jednog, pa ovdje nema izbora uloge.
    public string AdminFirstName { get; init; } = string.Empty;
    public string AdminLastName { get; init; } = string.Empty;
    public string AdminEmail { get; init; } = string.Empty;
    public string AdminUsername { get; init; } = string.Empty;
    public string AdminPassword { get; init; } = string.Empty;
}
