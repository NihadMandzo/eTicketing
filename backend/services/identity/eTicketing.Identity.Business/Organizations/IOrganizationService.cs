using System.Security.Claims;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Identity.Business.Organizations;

public interface IOrganizationService
{
    Task<Result<PagedResult<OrganizationResponse>>> GetAsync(OrganizationQuery query, CancellationToken ct = default);
    Task<Result<OrganizationResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Batch, internal-only lookup for eTicketing.Ticketing's Izvještaji reports — never
    /// routed through the Gateway. Unknown ids are simply absent from the result rather than an
    /// error: an organization deleted between a sale and the report that counts it should not fail
    /// the whole page.</summary>
    Task<Result<List<OrganizationInternalResponse>>> GetInternalByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);

    /// <summary>Contact details for one organization — see <see cref="OrganizationContactResponse"/>
    /// for why this is separate from the by-ids lookup above.</summary>
    Task<Result<OrganizationContactResponse>> GetInternalContactAsync(Guid id, CancellationToken ct = default);
    Task<Result<OrganizationResponse>> CreateAsync(CreateOrganizationRequest request, CancellationToken ct = default);
    Task<Result<OrganizationResponse>> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>First-time logo upload — fails with a Conflict if the organization already has one (use ReplaceLogoAsync instead).</summary>
    Task<Result<OrganizationResponse>> UploadLogoAsync(Guid id, IFormFile logo, CancellationToken ct = default);

    /// <summary>Replaces an existing logo in place (same blob key) — fails with NotFound if the organization has no logo yet (use UploadLogoAsync instead).</summary>
    Task<Result<OrganizationResponse>> ReplaceLogoAsync(Guid id, IFormFile logo, CancellationToken ct = default);

    /// <summary>caller drives the ownership check: platform staff can view any organization's
    /// users; anyone else may only view their own organization's (any org role, not just
    /// OrganizationSuperAdmin — this is a read, not a management action).</summary>
    Task<Result<PagedResult<UserResponse>>> GetUsersAsync(Guid organizationId, OrganizationUserQuery query, ClaimsPrincipal caller, CancellationToken ct = default);

    /// <summary>caller drives both the ownership check and the role restriction: platform staff
    /// may add either org role to any organization; an OrganizationSuperAdmin may only add an
    /// OrganizationAdmin to their own organization (enforced here, not just by the Organizer
    /// policy at the route level, since that policy alone can't express "own org only" or
    /// "OrganizationAdmin only").</summary>
    Task<Result<UserResponse>> AddUserAsync(Guid organizationId, AddOrganizationUserRequest request, ClaimsPrincipal caller, CancellationToken ct = default);

    /// <summary>Profile-only edit (no role/active-status change) of an existing organization
    /// user. Same ownership rule as AddUserAsync.</summary>
    Task<Result<UserResponse>> UpdateUserAsync(Guid organizationId, Guid userId, UpdateOrganizationUserRequest request, ClaimsPrincipal caller, CancellationToken ct = default);

    /// <summary>Same ownership rule as AddUserAsync. Deleting an OrganizationSuperAdmin is always
    /// blocked (see OrganizationService.RemoveUserAsync) — there's no transfer-ownership flow, so
    /// every organization must keep exactly one at all times.</summary>
    Task<Result> RemoveUserAsync(Guid organizationId, Guid userId, ClaimsPrincipal caller, CancellationToken ct = default);
}
