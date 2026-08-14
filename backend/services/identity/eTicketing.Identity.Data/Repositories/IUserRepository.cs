using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;

namespace eTicketing.Identity.Data.Repositories;

public interface IUserRepository : IRepository<User, Guid>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsByEmailOrUsernameAsync(string email, string username, CancellationToken ct = default);

    /// <summary>Username ownership check that excludes <paramref name="excludeUserId"/>, so a user renaming themselves back to their own current username never trips a "taken" conflict.</summary>
    Task<bool> ExistsByUsernameAsync(string username, Guid excludeUserId, CancellationToken ct = default);

    /// <summary>Paged, FTS-filtered (first name/last name/email) search over users with a given
    /// role — backs AdminService.GetAsync.</summary>
    Task<PagedResult<User>> SearchByRoleAsync(RoleType role, BaseSearchObject query, CancellationToken ct = default);

    /// <summary>Paged, FTS-filtered (first name/last name/email), optionally role-filtered users
    /// belonging to an organization — backs OrganizationService.GetUsersAsync. role is passed as
    /// a plain parameter rather than the Business-layer OrganizationUserQuery type so Data
    /// doesn't need to reference Business (would be circular).</summary>
    Task<PagedResult<User>> SearchByOrganizationAsync(Guid organizationId, BaseSearchObject query, RoleType? role, CancellationToken ct = default);

    /// <summary>Live count of an organization's users — backs OrganizationService.UpdateAsync,
    /// which needs a fresh count without re-loading every user row.</summary>
    Task<int> CountByOrganizationAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>All of an organization's users, unpaged — backs OrganizationService.DeleteAsync,
    /// which hard-deletes every user in the organization alongside it (no soft delete anywhere in
    /// this app, and the FK is Restrict — see UserConfiguration — so leaving them behind isn't an
    /// option).</summary>
    Task<List<User>> GetAllByOrganizationAsync(Guid organizationId, CancellationToken ct = default);
}
