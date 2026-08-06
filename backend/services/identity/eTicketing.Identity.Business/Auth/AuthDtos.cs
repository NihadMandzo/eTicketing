namespace eTicketing.Identity.Business.Auth;

public record RegisterRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
}

public record LoginRequest
{
    public string EmailOrUsername { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public record ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}

public record UpdateUserRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
}

public record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Username,
    string? PhoneNumber,
    string RoleName,
    Guid? OrganizationId,
    bool IsActive,
    bool IsEmailVerified,
    bool IsFirstLogin,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

/// <summary>The access/refresh tokens never appear in a response body — they're written
/// straight to httpOnly cookies (see AuthEndpoints). This is the client-facing payload.</summary>
public record LoginResponse(UserResponse User);

/// <summary>Internal Business→Api handoff only — never serialized directly.</summary>
public record LoginResult(UserResponse User, string AccessToken, string RefreshToken);
