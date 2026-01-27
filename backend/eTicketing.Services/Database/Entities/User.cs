namespace eTicketing.Services.Database.Entities;

public class User : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; } = false;
    public bool IsFirstLogin { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public string? OTP { get; set; }
    public DateTime? OTPExpiration { get; set; }
    
    // Foreign Keys
    public int RoleId { get; set; }
    public int? OrganizationId { get; set; }
    
    // Navigation Properties
    public Role Role { get; set; } = null!;
    public Organization? Organization { get; set; }
}
