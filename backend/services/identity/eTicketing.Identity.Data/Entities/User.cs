using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Enums;

namespace eTicketing.Identity.Data.Entities;

public class User : BaseEntity
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    public RoleType Role { get; set; }

    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; }
    public bool IsFirstLogin { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }
}
