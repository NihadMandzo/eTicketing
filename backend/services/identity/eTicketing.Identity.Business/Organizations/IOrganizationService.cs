using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Identity.Business.Organizations;

public interface IOrganizationService
{
    Task<Result<PagedResult<OrganizationResponse>>> GetAsync(OrganizationQuery query, CancellationToken ct = default);
    Task<Result<OrganizationResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<OrganizationResponse>> CreateAsync(CreateOrganizationRequest request, CancellationToken ct = default);
    Task<Result<OrganizationResponse>> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>First-time logo upload — fails with a Conflict if the organization already has one (use ReplaceLogoAsync instead).</summary>
    Task<Result<OrganizationResponse>> UploadLogoAsync(Guid id, IFormFile logo, CancellationToken ct = default);

    /// <summary>Replaces an existing logo in place (same blob key) — fails with NotFound if the organization has no logo yet (use UploadLogoAsync instead).</summary>
    Task<Result<OrganizationResponse>> ReplaceLogoAsync(Guid id, IFormFile logo, CancellationToken ct = default);

    Task<Result<PagedResult<UserResponse>>> GetUsersAsync(Guid organizationId, OrganizationUserQuery query, CancellationToken ct = default);
    Task<Result<UserResponse>> AddUserAsync(Guid organizationId, AddOrganizationUserRequest request, CancellationToken ct = default);
    Task<Result> RemoveUserAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
}
