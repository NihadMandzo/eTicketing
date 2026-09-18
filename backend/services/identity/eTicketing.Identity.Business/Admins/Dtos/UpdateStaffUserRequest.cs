using eTicketing.Contracts.Pagination;
using eTicketing.Identity.Business.Shared.Validators;
using eTicketing.Identity.Data.Enums;

namespace eTicketing.Identity.Business.Admins;

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
