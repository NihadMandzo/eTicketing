namespace eTicketing.Identity.Business.Auth;

public record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Username,
    string? PhoneNumber,
    string RoleName,
    Guid? OrganizationId,
    string? OrganizationName,
    bool IsActive,
    bool IsEmailVerified,
    bool IsFirstLogin,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime? LastLoginAt);
