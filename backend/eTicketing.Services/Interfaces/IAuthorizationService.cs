namespace eTicketing.Services.Interfaces;

public interface IAuthorizationService
{
    Task<bool> CanManageOrganizationAsync(int organizationId, CancellationToken cancellationToken = default);
    Task<bool> CanDeleteOrganizationAsync(int organizationId, CancellationToken cancellationToken = default);
    Task<bool> CanManageUserAsync(int userId, CancellationToken cancellationToken = default);
    Task ValidateOrganizationAccessAsync(int organizationId, CancellationToken cancellationToken = default);
    bool IsSuperAdmin();
    bool IsOrganizationSuperAdmin();
    bool IsOrganizationAdmin();
}
