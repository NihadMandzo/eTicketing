using eTicketing.Contracts.Pagination;
using eTicketing.Identity.Business.Shared.Validators;
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

    /// <summary>Manually typed by SuperAdmin at creation time — the recipient of the
    /// "organization created" notification email. Deliberately independent of AdminEmail: the
    /// account SuperAdmin is standing up (the org's first OrganizationSuperAdmin) is not
    /// necessarily who SuperAdmin wants to notify.</summary>
    public string NotificationEmail { get; init; } = string.Empty;

    // Podaci prvog organizatora — kreira se u istoj transakciji kao i organizacija. Uvijek
    // postaje OrganizationSuperAdmin (vidi OrganizationMappingConfig) — svaka organizacija mora
    // imati tačno jednog, pa ovdje nema izbora uloge.
    public string AdminFirstName { get; init; } = string.Empty;
    public string AdminLastName { get; init; } = string.Empty;
    public string AdminEmail { get; init; } = string.Empty;
    public string AdminUsername { get; init; } = string.Empty;
    public string AdminPassword { get; init; } = string.Empty;
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
}

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

/// <summary>LogoUrl is null until a logo has been uploaded via the dedicated
/// POST/PUT /organizations/{id}/logo endpoints — organizations no longer carry logo bytes at
/// all (see Organization.LogoBlobName); it's derived from Azure Blob Storage, not a stored
/// column.</summary>
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

/// <summary>
/// The shape served to signed-out visitors by GET /organizations/{id}/public — the "Organizator"
/// card the storefront renders on a product page (web and mobile both show name, address,
/// description, and email/phone as mailto:/tel: links).
///
/// <para>Contact details are deliberately kept: these are the organization's own published
/// business details, not any individual staff member's. What is deliberately dropped is
/// everything that only means something inside the back office —
/// <c>UserCount</c> (how many staff accounts an organization has), <c>IsActive</c> and
/// <c>CreatedAt</c>. Those were previously readable by anyone, unauthenticated, because
/// <see cref="OrganizationResponse"/> was served on an AllowAnonymous route.</para>
/// </summary>
public record OrganizationPublicResponse(
    Guid Id,
    string Name,
    string Description,
    string Address,
    string PhoneNumber,
    string Email,
    string? Website,
    string? LogoUrl);

/// <summary>Plain mutable class, not a record — carries an IFormFile, bound via [FromForm].
/// Shared shape for both POST (create) and PUT (replace) /organizations/{id}/logo.</summary>
public class OrganizationLogoUploadRequest
{
    public IFormFile Logo { get; set; } = null!;
}

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
