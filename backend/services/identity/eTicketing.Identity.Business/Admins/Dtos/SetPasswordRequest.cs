namespace eTicketing.Identity.Business.Admins;

/// <summary>SuperAdmin directly sets a staff/organization account's password (no current
/// password needed — see AdminService.SetPasswordAsync). Deliberately excludes User and
/// SuperAdmin targets.</summary>
public sealed record SetPasswordRequest
{
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}
