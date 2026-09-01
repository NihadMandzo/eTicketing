using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Catalog.Data.Repositories;

public class ProductRepository : Repository<Product, Guid>, IProductRepository
{
    public ProductRepository(CatalogDbContext context) : base(context) { }

    public Task<PagedResult<Product>> SearchAsync(
        BaseSearchObject query, Guid? organizationId, int? categoryId, PublishStatus? status, City? city, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => string.IsNullOrEmpty(query.FTS) || p.Name.Contains(query.FTS))
            .Where(p => organizationId == null || p.OrganizationId == organizationId)
            .Where(p => categoryId == null || p.CategoryId == categoryId)
            .Where(p => status == null || p.Status == status)
            .Where(p => city == null || p.City == city)
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
        => Query().Include(p => p.Category).Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<Product>> GetByIdsWithCategoryAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(ct);

    public Task<List<Product>> GetPublishedByIdsWithDetailsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => ids.Contains(p.Id) && p.Status == PublishStatus.Published)
            .ToListAsync(ct);

    public Task<List<Product>> GetPublishedCandidatesAsync(int take, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Where(p => p.Status == PublishStatus.Published)
            .OrderByDescending(p => p.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public Task<List<Product>> GetUpcomingAsync(
        Guid? organizationId, DateTime nowUtc, int count, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.Status == PublishStatus.Published)
            .Where(p => p.Category!.TicketingMode == TicketingMode.SingleOccurrence)
            .Where(p => p.Date != null && p.Date >= nowUtc)
            .Where(p => organizationId == null || p.OrganizationId == organizationId)
            .OrderBy(p => p.Date)
            .Take(count)
            .ToListAsync(ct);

    public Task<List<OrganizationProductStats>> GetOrganizationStatsAsync(CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .GroupBy(p => p.OrganizationId)
            .Select(g => new OrganizationProductStats(
                g.Key,
                g.Count(),
                g.Count(p => p.Status == PublishStatus.Published),
                g.Count(p => p.Status == PublishStatus.Draft),
                // !p.Images.Any() rather than a left join with a null check: this translates to a
                // NOT EXISTS subquery, which stays correct whether a product has zero photos or five.
                g.Count(p => !p.Images.Any())))
            .ToListAsync(ct);
}
