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

    /// <summary>Paged users belonging to an organization — backs OrganizationService.GetUsersAsync.</summary>
    Task<PagedResult<User>> SearchByOrganizationAsync(Guid organizationId, BaseSearchObject query, CancellationToken ct = default);

    /// <summary>Live count of an organization's users — backs OrganizationService.UpdateAsync,
    /// which needs a fresh count without re-loading every user row.</summary>
    Task<int> CountByOrganizationAsync(Guid organizationId, CancellationToken ct = default);
}
