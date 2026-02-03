using eTicketing.Model.Requests;
using eTicketing.Model.Responses;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Interfaces.Shared;

namespace eTicketing.Services.Interfaces;

public interface IOrganizationService : ICRUDService<OrganizationResponse, OrganizationSearchObject, 
    OrganizationInsertRequest, OrganizationUpdateRequest>
{
    Task<OrganizationDetailResponse> GetByIdDetailedAsync(int id, CancellationToken cancellationToken = default);
    Task<UserResponse> AddUserAsync(int organizationId, OrganizationUserRequest request, CancellationToken cancellationToken = default);
    Task<bool> RemoveUserAsync(int organizationId, int userId, CancellationToken cancellationToken = default);
    Task<List<UserResponse>> GetOrganizationUsersAsync(int organizationId, CancellationToken cancellationToken = default);
}
