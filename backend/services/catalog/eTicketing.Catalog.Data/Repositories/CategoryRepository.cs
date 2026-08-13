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
            .ToPagedResultAsync(query.Page, query.PageSize, ct);
}
