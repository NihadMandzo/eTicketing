using eTicketing.Contracts.Pagination;
using eTicketing.Identity.Business.Shared.Validators;
using eTicketing.Identity.Data.Enums;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Identity.Business.Organizations;

public record AddOrganizationUserRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    /// <summary>Must be OrganizationSuperAdmin or OrganizationAdmin — validated in RoleValidator.
    /// A caller that isn't platform staff (i.e. an OrganizationSuperAdmin self-servicing their
    /// own org) is further restricted to OrganizationAdmin only — see
    /// OrganizationService.AddUserAsync.</summary>
    public RoleType Role { get; init; } = RoleType.OrganizationAdmin;
}
