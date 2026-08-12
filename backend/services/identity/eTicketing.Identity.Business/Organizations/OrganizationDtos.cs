using eTicketing.Contracts.Pagination;
using eTicketing.Identity.Data.Enums;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Identity.Business.Organizations;

public record CreateOrganizationRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Website { get; init; }

    // Podaci prvog organizatora — kreira se u istoj transakciji kao i organizacija
    public string AdminFirstName { get; init; } = string.Empty;
    public string AdminLastName { get; init; } = string.Empty;
    public string AdminEmail { get; init; } = string.Empty;
    public string AdminUsername { get; init; } = string.Empty;
    public string AdminPassword { get; init; } = string.Empty;

    /// <summary>Must be OrganizationSuperAdmin or OrganizationAdmin — validated in AdminRoleValidator.</summary>
    public RoleType AdminRole { get; init; } = RoleType.OrganizationSuperAdmin;

    /// <summary>Optional — PNG/JPEG, validated by OrganizationLogoValidation. Bound via
    /// [FromForm]; this endpoint (and Update) is multipart/form-data specifically so a logo can
    /// be attached alongside the text fields, same shape as Category's icon upload.</summary>
    public IFormFile? Logo { get; init; }
}

public record UpdateOrganizationRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Website { get; init; }
    public bool IsActive { get; init; } = true;

    /// <summary>Optional replacement logo — omit to keep the existing one.</summary>
    public IFormFile? Logo { get; init; }

    /// <summary>Explicitly clears the existing logo when true (and no replacement Logo is
    /// supplied) — mirrors Category's update-without-touching-icon default, but organizations
    /// need an explicit "remove" signal since a logo is optional in the first place.</summary>
    public bool RemoveLogo { get; init; }
}

public record AddOrganizationUserRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    /// <summary>Must be OrganizationSuperAdmin or OrganizationAdmin — validated in RoleValidator.</summary>
    public RoleType Role { get; init; } = RoleType.OrganizationAdmin;
}

public record OrganizationResponse(
    Guid Id,
    string Name,
    string Description,
    string Address,
    string PhoneNumber,
    string Email,
    string? Website,
    string? LogoUrl,
    bool IsActive,
    int UserCount,
    DateTime CreatedAt);

/// <summary>Raw logo bytes + content-type, returned by OrganizationService.GetLogoAsync and
/// streamed as-is by GET /organizations/{id}/logo.</summary>
public record OrganizationLogo(byte[] Data, string ContentType);

public sealed record OrganizationQuery : BaseSearchObject
{
    /// <summary>Set only when the desktop app has pre-resolved organization ids from the
    /// category multiselect filter (via Catalog's GET /events/organization-ids) — Organizations
    /// and Events/Categories live in separate microservices/databases, so this filter can't be
    /// applied as a join here, only as an explicit id list supplied by the caller.
    /// Guid[] (not List&lt;Guid&gt;) is required — minimal APIs' query-string binder only
    /// special-cases arrays of parsable types for multi-value query params
    /// (?OrganizationIds=x&amp;OrganizationIds=y); List&lt;Guid&gt; has no TryParse and crashes
    /// the app at startup ("must have a valid TryParse method").</summary>
    public Guid[]? OrganizationIds { get; init; }
}

/// <summary>Query for GET /organizations/{id}/users. Was previously a plain BaseSearchObject
/// (see GetUsers/GetUsersAsync/SearchByOrganizationAsync) — that meant FTS silently had no
/// effect (SearchByOrganizationAsync never read it) and there was no way to narrow by role, so
/// the admins/superadmins sub-lists on the organization detail screen couldn't be told apart.</summary>
public sealed record OrganizationUserQuery : BaseSearchObject
{
    public RoleType? Role { get; init; }
}
