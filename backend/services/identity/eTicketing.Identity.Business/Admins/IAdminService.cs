using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;

namespace eTicketing.Identity.Business.Admins;

public interface IAdminService
{
    Task<Result<PagedResult<UserResponse>>> GetAsync(AdminQuery query, CancellationToken ct = default);
    Task<Result<UserResponse>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<UserResponse>> CreateAsync(CreateAdminRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}
