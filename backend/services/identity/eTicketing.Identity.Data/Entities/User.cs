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

    /// <summary>Set when SuperAdmin directly sets this account's password (staff/org accounts
    /// only — see AdminService.SetPasswordAsync). Forces a password change on next login instead
    /// of trusting the SuperAdmin-assigned password indefinitely.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>6-char code emailed on registration/resend, cleared once <see cref="IsEmailVerified"/>
    /// is set — so a code can never be reused after a successful verification.</summary>
    public string? EmailVerificationCode { get; set; }
    public DateTime? EmailVerificationCodeExpiresAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
}
