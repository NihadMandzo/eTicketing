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

    /// <summary>Organizations by id, WITHOUT Users — backs the internal
    /// POST /internal/organizations/by-ids that eTicketing.Ticketing's Izvještaji reports call to
    /// label their rows. Deliberately skips the Users include the two methods above need: that
    /// caller wants a name and an address, and pulling every staff row per organization for it
    /// would be pure waste. Unknown ids are simply absent from the result.</summary>
    Task<List<Organization>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
}
