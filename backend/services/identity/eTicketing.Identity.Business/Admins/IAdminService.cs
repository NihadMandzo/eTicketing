using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;
using eTicketing.Identity.Business.Auth;

namespace eTicketing.Identity.Business.Admins;

public interface IAdminService
{
    Task<Result<PagedResult<UserResponse>>> GetAsync(AdminQuery query, CancellationToken ct = default);
    Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<UserResponse>> CreateAsync(CreateAdminRequest request, CancellationToken ct = default);
    Task<Result<UserResponse>> UpdateAsync(Guid id, UpdateStaffUserRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, DeleteAdminRequest request, CancellationToken ct = default);
    Task<Result> SetPasswordAsync(Guid id, SetPasswordRequest request, CancellationToken ct = default);
}
