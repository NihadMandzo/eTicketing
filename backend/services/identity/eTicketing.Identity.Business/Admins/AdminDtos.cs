using eTicketing.Contracts.Pagination;
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
public sealed record UpdateStaffUserRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
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
