using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Catalog.Data.Repositories;

public class ProductRepository : Repository<Product, Guid>, IProductRepository
{
    public ProductRepository(CatalogDbContext context) : base(context) { }

    public Task<PagedResult<Product>> SearchAsync(
        BaseSearchObject query, Guid? organizationId, int? categoryId, PublishStatus? status, CancellationToken ct = default)
        => Query()
            .Include(p => p.Category)
            .Where(p => string.IsNullOrEmpty(query.FTS) || p.Name.Contains(query.FTS))
            .Where(p => organizationId == null || p.OrganizationId == organizationId)
            .Where(p => categoryId == null || p.CategoryId == categoryId)
            .Where(p => status == null || p.Status == status)
            .OrderByDescending(p => p.CreatedAt)
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

    public Task<List<Guid>> GetOrganizationIdsByCategoryIdsAsync(IReadOnlyList<int> categoryIds, CancellationToken ct = default)
        => Query()
            .Where(p => categoryIds.Contains(p.CategoryId))
            .Select(p => p.OrganizationId)
            .Distinct()
            .ToListAsync(ct);

    public Task<bool> ExistsForCategoryAsync(int categoryId, CancellationToken ct = default)
        => Query().AnyAsync(p => p.CategoryId == categoryId, ct);

    public Task<Product?> GetByIdWithCategoryAsync(Guid id, CancellationToken ct = default)
        => Query().Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id, ct);
}
