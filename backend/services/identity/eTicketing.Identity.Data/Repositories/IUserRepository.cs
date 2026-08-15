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

    /// <summary>Email+username ownership check that excludes <paramref name="excludeUserId"/> —
    /// used by the new admin-edits-another-user flows (AdminService.UpdateAsync,
    /// OrganizationService.UpdateUserAsync), which unlike self-service UpdateUserAsync also allow
    /// changing Email, so both fields need the same self-exclusion as ExistsByUsernameAsync.</summary>
    Task<bool> ExistsByEmailOrUsernameAsync(string email, string username, Guid excludeUserId, CancellationToken ct = default);

    /// <summary>Whether an organization already has a user with the given role — used to enforce
    /// "exactly one OrganizationSuperAdmin per organization" before inserting a second one.</summary>
    Task<bool> ExistsByOrganizationAndRoleAsync(Guid organizationId, RoleType role, CancellationToken ct = default);

    /// <summary>Same as the base <see cref="eTicketing.Contracts.Persistence.IRepository{T,TKey}.GetByIdAsync"/>
    /// but with <c>Organization</c> included, for the call sites that actually surface
    /// <see cref="eTicketing.Identity.Business.Auth.UserResponse.OrganizationName"/> to a caller
    /// (the base lookup uses DbSet.FindAsync, which never includes navigation properties).</summary>
    Task<User?> GetByIdWithOrganizationAsync(Guid id, CancellationToken ct = default);

    /// <summary>Paged, FTS-filtered (first name/last name/email) search over every non-buyer
    /// (Role != User) account — platform staff + organization accounts — optionally narrowed to
    /// any subset of roles (multiselect). Backs AdminService.GetAsync (the SuperAdmin-facing
    /// "platform Users" list). Includes Organization so OrganizationName is populated for
    /// org-scoped roles. Null or empty roleFilters means "every role" — same
    /// absent-array-binds-to-empty-not-null convention as OrganizationRepository.SearchAsync.</summary>
    Task<PagedResult<User>> SearchStaffAsync(IReadOnlyList<RoleType>? roleFilters, BaseSearchObject query, CancellationToken ct = default);

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
