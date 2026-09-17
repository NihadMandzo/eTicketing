using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;
using eTicketing.Identity.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Identity.Data.Repositories;

public class UserRepository : Repository<User, Guid>, IUserRepository
{
    public UserRepository(IdentityDbContext context) : base(context) { }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => Query().Include(u => u.Organization).FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => Query().Include(u => u.Organization).FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<bool> ExistsByEmailOrUsernameAsync(string email, string username, CancellationToken ct = default)
        => Query().AnyAsync(u => u.Email == email || u.Username == username, ct);

    public Task<bool> ExistsByUsernameAsync(string username, Guid excludeUserId, CancellationToken ct = default)
        => Query().AnyAsync(u => u.Username == username && u.Id != excludeUserId, ct);

    public Task<bool> ExistsByEmailOrUsernameAsync(string email, string username, Guid excludeUserId, CancellationToken ct = default)
        => Query().AnyAsync(u => u.Id != excludeUserId && (u.Email == email || u.Username == username), ct);

    public Task<bool> ExistsByOrganizationAndRoleAsync(Guid organizationId, RoleType role, CancellationToken ct = default)
        => Query().AnyAsync(u => u.OrganizationId == organizationId && u.Role == role, ct);

    public Task<string?> GetEmailByOrganizationAndRoleAsync(Guid organizationId, RoleType role, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Where(u => u.OrganizationId == organizationId && u.Role == role)
            .Select(u => (string?)u.Email)
            .FirstOrDefaultAsync(ct);

    public Task<User?> GetByIdWithOrganizationAsync(Guid id, CancellationToken ct = default)
        => Query().Include(u => u.Organization).FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<PagedResult<User>> SearchStaffAsync(IReadOnlyList<RoleType>? roleFilters, BaseSearchObject query, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Include(u => u.Organization)
            .Where(u => u.Role != RoleType.User)
            // Minimal APIs bind an absent array-typed query param to an empty array, not null —
            // roleFilters.Count == 0 must also mean "no filter", same convention as
            // OrganizationRepository.SearchAsync's organizationIds handling.
            .Where(u => roleFilters == null || roleFilters.Count == 0 || roleFilters.Contains(u.Role))
            .Where(u => string.IsNullOrEmpty(query.FTS)
                || u.FirstName.Contains(query.FTS) || u.LastName.Contains(query.FTS) || u.Email.Contains(query.FTS))
            .OrderBy(u => u.LastName)
            .ToPagedResultAsync(query.EffectivePage, query.EffectivePageSize, ct);

    public Task<PagedResult<User>> SearchByOrganizationAsync(Guid organizationId, BaseSearchObject query, RoleType? role, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Include(u => u.Organization)
            .Where(u => u.OrganizationId == organizationId)
            .Where(u => role == null || u.Role == role)
            .Where(u => string.IsNullOrEmpty(query.FTS)
                || u.FirstName.Contains(query.FTS) || u.LastName.Contains(query.FTS) || u.Email.Contains(query.FTS))
            .OrderBy(u => u.LastName)
            .ToPagedResultAsync(query.EffectivePage, query.EffectivePageSize, ct);

    public Task<int> CountByOrganizationAsync(Guid organizationId, CancellationToken ct = default)
        => Query().CountAsync(u => u.OrganizationId == organizationId, ct);

    public Task<List<User>> GetAllByOrganizationAsync(Guid organizationId, CancellationToken ct = default)
        => Query().Where(u => u.OrganizationId == organizationId).ToListAsync(ct);
}
