using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Catalog.Data.Repositories;

public class CategoryRepository : Repository<Category, int>, ICategoryRepository
{
    public CategoryRepository(CatalogDbContext context) : base(context) { }

    public Task<PagedResult<Category>> SearchAsync(BaseSearchObject query, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Where(c => string.IsNullOrEmpty(query.FTS) || c.Name.Contains(query.FTS))
            .Where(c => query.IsActive == null || c.IsActive == query.IsActive)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToPagedResultAsync(query.EffectivePage, query.EffectivePageSize, ct);

    // Compared lowercased rather than with a plain ==: SQL Server's default collation is
    // case-insensitive while Sqlite (the test fixtures) is not, so a plain comparison would mean
    // this check behaved differently in tests than in production. The unique index in
    // CategoryConfiguration is still the real guarantee — this is the friendly error before it.
    public Task<bool> ExistsByNameAsync(string name, int? excludeId = null, CancellationToken ct = default)
        => Query().AnyAsync(
            c => (excludeId == null || c.Id != excludeId) && c.Name.ToLower() == name.ToLower(),
            ct);
}
