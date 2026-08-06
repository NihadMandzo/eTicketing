using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;

namespace eTicketing.Identity.Business.Organizations;

public interface IOrganizationService
{
    Task<Result<PagedResult<OrganizationResponse>>> GetAsync(OrganizationQuery query, CancellationToken ct = default);
    Task<Result<OrganizationResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<OrganizationResponse>> CreateAsync(CreateOrganizationRequest request, CancellationToken ct = default);
    Task<Result<OrganizationResponse>> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<Result<PagedResult<UserResponse>>> GetUsersAsync(Guid organizationId, BaseSearchObject query, CancellationToken ct = default);
    Task<Result<UserResponse>> AddUserAsync(Guid organizationId, AddOrganizationUserRequest request, CancellationToken ct = default);
    Task<Result> RemoveUserAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
}
