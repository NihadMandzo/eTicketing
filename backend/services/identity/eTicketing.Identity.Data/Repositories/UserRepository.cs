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
        => Query().FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => Query().FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<bool> ExistsByEmailOrUsernameAsync(string email, string username, CancellationToken ct = default)
        => Query().AnyAsync(u => u.Email == email || u.Username == username, ct);

    public Task<bool> ExistsByUsernameAsync(string username, Guid excludeUserId, CancellationToken ct = default)
        => Query().AnyAsync(u => u.Username == username && u.Id != excludeUserId, ct);

    public Task<PagedResult<User>> SearchByRoleAsync(RoleType role, BaseSearchObject query, CancellationToken ct = default)
        => Query()
            .Where(u => u.Role == role)
            .Where(u => string.IsNullOrEmpty(query.FTS)
                || u.FirstName.Contains(query.FTS) || u.LastName.Contains(query.FTS) || u.Email.Contains(query.FTS))
            .OrderBy(u => u.LastName)
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public Task<PagedResult<User>> SearchByOrganizationAsync(Guid organizationId, BaseSearchObject query, RoleType? role, CancellationToken ct = default)
        => Query()
            .Where(u => u.OrganizationId == organizationId)
            .Where(u => role == null || u.Role == role)
            .Where(u => string.IsNullOrEmpty(query.FTS)
                || u.FirstName.Contains(query.FTS) || u.LastName.Contains(query.FTS) || u.Email.Contains(query.FTS))
            .OrderBy(u => u.LastName)
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public Task<int> CountByOrganizationAsync(Guid organizationId, CancellationToken ct = default)
        => Query().CountAsync(u => u.OrganizationId == organizationId, ct);
}
