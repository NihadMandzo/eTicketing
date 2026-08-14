using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;

namespace eTicketing.Identity.Data.Repositories;

public interface IOrganizationRepository : IRepository<Organization, Guid>
{
    /// <summary>Paged, FTS-filtered (name) search over organizations, with Users loaded so the
    /// business layer can map each row's live user count — backs OrganizationService.GetAsync.
    /// organizationIds (from OrganizationQuery.OrganizationIds) is passed as a plain parameter
    /// rather than the Business-layer OrganizationQuery type so Data doesn't need to reference
    /// Business (would be circular — Business already references Data).</summary>
    Task<PagedResult<Organization>> SearchAsync(BaseSearchObject query, IReadOnlyList<Guid>? organizationIds, CancellationToken ct = default);

    /// <summary>Single organization with Users loaded, for the same user-count mapping reason
    /// as <see cref="SearchAsync"/> — backs OrganizationService.GetByIdAsync.</summary>
    Task<Organization?> GetByIdWithUsersAsync(Guid id, CancellationToken ct = default);
}
