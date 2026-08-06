using eTicketing.Contracts.Pagination;
using eTicketing.Identity.Data.Enums;

namespace eTicketing.Identity.Business.Organizations;

public record CreateOrganizationRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Website { get; init; }

    // Podaci prvog organizatora — kreira se u istoj transakciji kao i organizacija
    public string AdminFirstName { get; init; } = string.Empty;
    public string AdminLastName { get; init; } = string.Empty;
    public string AdminEmail { get; init; } = string.Empty;
    public string AdminUsername { get; init; } = string.Empty;
    public string AdminPassword { get; init; } = string.Empty;

    /// <summary>Must be OrganizationSuperAdmin or OrganizationAdmin — validated in AdminRoleValidator.</summary>
    public RoleType AdminRole { get; init; } = RoleType.OrganizationSuperAdmin;
}

public record UpdateOrganizationRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Website { get; init; }
    public bool IsActive { get; init; } = true;
}

public record AddOrganizationUserRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    /// <summary>Must be OrganizationSuperAdmin or OrganizationAdmin — validated in RoleValidator.</summary>
    public RoleType Role { get; init; } = RoleType.OrganizationAdmin;
}

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

public sealed record OrganizationQuery : BaseSearchObject;
