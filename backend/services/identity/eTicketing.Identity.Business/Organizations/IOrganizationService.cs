using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;

namespace eTicketing.Identity.Business.Organizations;

public interface IOrganizationService
{
    Task<Result<PagedResult<OrganizationResponse>>> GetAsync(OrganizationQuery query, CancellationToken ct = default);
    Task<Result<OrganizationResponse>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<OrganizationResponse>> CreateAsync(CreateOrganizationRequest request, CancellationToken ct = default);
    Task<Result<OrganizationResponse>> UpdateAsync(int id, UpdateOrganizationRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);

    Task<Result<PagedResult<UserResponse>>> GetUsersAsync(int organizationId, BaseSearchObject query, CancellationToken ct = default);
    Task<Result<UserResponse>> AddUserAsync(int organizationId, AddOrganizationUserRequest request, CancellationToken ct = default);
    Task<Result> RemoveUserAsync(int organizationId, int userId, CancellationToken ct = default);
}
