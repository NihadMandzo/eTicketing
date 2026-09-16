namespace eTicketing.Identity.Business.Auth;

/// <summary>Used by the forced-password-change flow (SuperAdmin set a staff/org account's
/// password directly — see AdminService.SetPasswordAsync — and MustChangePassword is true).
/// Unlike ChangePasswordRequest this needs no CurrentPassword: the caller is already
/// authenticated with the SuperAdmin-assigned password and is replacing it with their own.</summary>
public record SetNewPasswordRequest
{
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}
