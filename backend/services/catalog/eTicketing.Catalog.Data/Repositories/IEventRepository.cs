using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Repositories;

public interface IEventRepository : IRepository<Event, Guid>
{
    /// <summary>Paged, FTS-filtered (name) search, optionally narrowed by organization and/or
    /// category — backs EventService.GetAllAsync. organizationId/categoryId are passed as plain
    /// parameters rather than the Business-layer EventQuery type so Data doesn't need to
    /// reference Business (would be circular — Business already references Data).</summary>
    Task<PagedResult<Event>> SearchAsync(
        BaseSearchObject query, Guid? organizationId, int? categoryId, CancellationToken ct = default);

    /// <summary>Distinct OrganizationIds among events whose CategoryId is in <paramref name="categoryIds"/>,
    /// any status — backs the org-list category multiselect filter (GET /events/organization-ids).</summary>
    Task<List<Guid>> GetOrganizationIdsByCategoryIdsAsync(IReadOnlyList<int> categoryIds, CancellationToken ct = default);

    /// <summary>Whether any event (any status) still references this category — backs
    /// CategoryService.DeleteAsync's proactive FK-conflict check.</summary>
    Task<bool> ExistsForCategoryAsync(int categoryId, CancellationToken ct = default);
}
