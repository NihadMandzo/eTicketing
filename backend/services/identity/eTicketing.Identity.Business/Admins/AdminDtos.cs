using eTicketing.Contracts.Pagination;
using eTicketing.Identity.Business.Shared.Validators;
using eTicketing.Identity.Data.Enums;

namespace eTicketing.Identity.Business.Admins;

public record CreateAdminRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
}

/// <summary>Profile-only edit of an existing staff account (SuperAdmin, Admin,
/// OrganizationSuperAdmin or OrganizationAdmin) — role and active status are deliberately not
/// editable here, per the confirmed scope for this feature.</summary>
public sealed record UpdateStaffUserRequest : IStaffProfileRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
}

/// <summary>Body for DELETE /admins/{id}. Reason/RecipientEmail are only required when the
/// target is an OrganizationAdmin (enforced in AdminService.DeleteAsync, which is the only place
/// that knows the target's role) — always send this body, even empty, since the route no longer
/// accepts a bare DELETE.</summary>
public sealed record DeleteAdminRequest
{
    public string? Reason { get; init; }
    public string? RecipientEmail { get; init; }
}

/// <summary>SuperAdmin directly sets a staff/organization account's password (no current
/// password needed — see AdminService.SetPasswordAsync). Deliberately excludes User and
/// SuperAdmin targets.</summary>
public sealed record SetPasswordRequest
{
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}

/// <summary>Despite the "Admin" name (kept for route/backwards compatibility — see
/// AdminEndpoints), this now spans every non-buyer role: SuperAdmin, Admin,
/// OrganizationSuperAdmin, OrganizationAdmin. RoleFilters narrows to any subset of those (the
/// desktop role filter is a multiselect); null or empty returns all of them — same "absent
/// array-typed query param binds to an empty array, not null, so both must mean 'no filter'"
/// convention as OrganizationQuery.OrganizationIds.</summary>
public sealed record AdminQuery : BaseSearchObject
{
    public RoleType[]? RoleFilters { get; init; }
}
