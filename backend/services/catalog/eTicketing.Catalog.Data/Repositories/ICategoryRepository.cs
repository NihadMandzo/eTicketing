using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;

namespace eTicketing.Catalog.Data.Repositories;

public interface ICategoryRepository : IRepository<Category, int>
{
    /// <summary>Paged, FTS-filtered (name) + IsActive search — backs CategoryService.GetAsync.
    /// Takes the shared BaseSearchObject directly (not the Business-layer CategoryQuery, which
    /// adds no extra fields) so Data doesn't need to reference Business — see
    /// eTicketing.Identity.Data.Repositories.IOrganizationRepository.SearchAsync for the same
    /// pattern in the sibling service.</summary>
    Task<PagedResult<Category>> SearchAsync(BaseSearchObject query, CancellationToken ct = default);
}
