using System.ComponentModel.DataAnnotations;
using eTicketing.Contracts.Pagination;

namespace eTicketing.Identity.Business.Organizations;

public class CreateOrganizationRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(500)]
    public string Address { get; set; } = string.Empty;

    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress, StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Url]
    public string? Website { get; set; }

    // Podaci prvog organizatora — kreira se u istoj transakciji kao i organizacija
    [Required, StringLength(100, MinimumLength = 2)]
    public string AdminFirstName { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string AdminLastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(255)]
    public string AdminEmail { get; set; } = string.Empty;

    [Required, StringLength(50, MinimumLength = 3)]
    public string AdminUsername { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string AdminPassword { get; set; } = string.Empty;

    /// <summary>3 = OrganizationSuperAdmin, 4 = OrganizationAdmin</summary>
    [Range(3, 4)]
    public int AdminRoleId { get; set; } = 3;
}

public class UpdateOrganizationRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(500)]
    public string Address { get; set; } = string.Empty;

    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress, StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Url]
    public string? Website { get; set; }

    public bool IsActive { get; set; } = true;
}

public class AddOrganizationUserRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>3 = OrganizationSuperAdmin, 4 = OrganizationAdmin</summary>
    [Range(3, 4)]
    public int RoleId { get; set; } = 4;
}

public record OrganizationResponse(
    int Id,
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

public class OrganizationQuery : BaseSearchObject
{
}
