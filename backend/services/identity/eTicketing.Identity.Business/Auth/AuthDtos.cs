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

public record VerifyEmailRequest
{
    public string Code { get; init; } = string.Empty;
}

public record ForgotPasswordRequest
{
    public string Email { get; init; } = string.Empty;
}

public record ResetPasswordRequest
{
    public string Token { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}

/// <summary>Used by the forced-password-change flow (SuperAdmin set a staff/org account's
/// password directly — see AdminService.SetPasswordAsync — and MustChangePassword is true).
/// Unlike ChangePasswordRequest this needs no CurrentPassword: the caller is already
/// authenticated with the SuperAdmin-assigned password and is replacing it with their own.</summary>
public record SetNewPasswordRequest
{
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
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
    string? OrganizationName,
    bool IsActive,
    bool IsEmailVerified,
    bool IsFirstLogin,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

/// <summary>The access/refresh tokens never appear in a response body — they're written
/// straight to httpOnly cookies (see AuthEndpoints). This is the client-facing payload.</summary>
public record LoginResponse(UserResponse User);

/// <summary>Internal Business→Api handoff only — never serialized directly.</summary>
public record LoginResult(UserResponse User, string AccessToken, string RefreshToken);
