using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Repositories;

public interface IProductRepository : IRepository<Product, Guid>
{
    /// <summary>Paged, FTS-filtered (name) search, optionally narrowed by organization, category
    /// and/or status — backs ProductService.GetPublishedAsync/GetMineAsync/GetAllAsync.
    /// organizationId/categoryId/status are passed as plain parameters rather than the
    /// Business-layer ProductQuery type so Data doesn't need to reference Business (would be
    /// circular — Business already references Data).</summary>
    Task<PagedResult<Product>> SearchAsync(
        BaseSearchObject query, Guid? organizationId, int? categoryId, PublishStatus? status, CancellationToken ct = default);

    /// <summary>Distinct OrganizationIds among products whose CategoryId is in <paramref name="categoryIds"/>,
    /// any status — backs the org-list category multiselect filter (GET /products/organization-ids).</summary>
    Task<List<Guid>> GetOrganizationIdsByCategoryIdsAsync(IReadOnlyList<int> categoryIds, CancellationToken ct = default);

    /// <summary>Whether any product (any status) still references this category — backs
    /// CategoryService.DeleteAsync's proactive FK-conflict check.</summary>
    Task<bool> ExistsForCategoryAsync(int categoryId, CancellationToken ct = default);

    /// <summary>Loads a product together with its Category — needed to resolve the owning
    /// Category's TicketingMode (e.g. for Ticketing's internal ownership/mode lookup).</summary>
    Task<Product?> GetByIdWithCategoryAsync(Guid id, CancellationToken ct = default);
}
