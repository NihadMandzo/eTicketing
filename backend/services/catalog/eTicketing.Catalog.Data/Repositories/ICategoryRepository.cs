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

    /// <summary>Case-insensitive name-uniqueness check, mirroring
    /// eTicketing.Identity's IUserRepository.ExistsByEmailOrUsernameAsync.</summary>
    /// <param name="excludeId">The category being edited, so renaming it to its own name is not a
    /// conflict with itself. Omit when creating.</param>
    Task<bool> ExistsByNameAsync(string name, int? excludeId = null, CancellationToken ct = default);
}
