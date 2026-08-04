using System.ComponentModel.DataAnnotations;

namespace eTicketing.Identity.Business.Auth;

public class RegisterRequest
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

    [Phone]
    public string? PhoneNumber { get; set; }
}

public class LoginRequest
{
    [Required]
    public string EmailOrUsername { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public record UserResponse(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string Username,
    string? PhoneNumber,
    string RoleName,
    int? OrganizationId,
    bool IsActive,
    bool IsEmailVerified,
    bool IsFirstLogin,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public record LoginResponse(string Token, UserResponse User);
