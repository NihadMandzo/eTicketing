using eTicketing.Contracts.Pagination;
using eTicketing.Identity.Business.Shared.Validators;
using eTicketing.Identity.Data.Enums;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Identity.Business.Organizations;

/// <summary>Profile-only edit of an existing organization user — role isn't editable here (see
/// AddOrganizationUserRequest's doc comment on why the org's roles can't just be reassigned
/// freely).</summary>
public sealed record UpdateOrganizationUserRequest : IStaffProfileRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
}
