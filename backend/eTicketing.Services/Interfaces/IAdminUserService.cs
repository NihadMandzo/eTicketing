using eTicketing.Model.Responses;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Interfaces.Shared;

namespace eTicketing.Services.Interfaces;

public interface IAdminUserService : IService<UserResponse, AdminUserSearchObject>
{
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
