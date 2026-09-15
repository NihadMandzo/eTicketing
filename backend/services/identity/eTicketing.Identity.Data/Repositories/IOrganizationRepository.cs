using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Identity.Data.Entities;

namespace eTicketing.Identity.Data.Repositories;

public interface IOrganizationRepository : IRepository<Organization, Guid>
{
    /// <summary>Paged, FTS-filtered (name) search over organizations, each paired with its user
    /// count — backs OrganizationService.GetAsync.
    ///
    /// The count is computed in SQL rather than by loading Users: this used to Include them purely
    /// so the mapping could read Users.Count, which meant a ten-organization page with fifty staff
    /// each pulled five hundred user rows across the wire to produce ten integers.
    ///
    /// organizationIds (from OrganizationQuery.OrganizationIds) is passed as a plain parameter
    /// rather than the Business-layer OrganizationQuery type so Data doesn't need to reference
    /// Business (would be circular — Business already references Data).</summary>
    Task<PagedResult<OrganizationWithUserCount>> SearchAsync(BaseSearchObject query, IReadOnlyList<Guid>? organizationIds, CancellationToken ct = default);

    /// <summary>Single organization with Users loaded — GetByIdAsync maps the user count off the
    /// collection, and GetInternalContactAsync needs the rows themselves to find the
    /// OrganizationSuperAdmin. One organization's staff is a bounded read, unlike a whole page of
    /// them, so this one keeps the Include.</summary>
    Task<Organization?> GetByIdWithUsersAsync(Guid id, CancellationToken ct = default);

    /// <summary>Organizations by id, WITHOUT Users — backs the internal
    /// POST /internal/organizations/by-ids that eTicketing.Ticketing's Izvještaji reports call to
    /// label their rows. Deliberately skips the Users include the two methods above need: that
    /// caller wants a name and an address, and pulling every staff row per organization for it
    /// would be pure waste. Unknown ids are simply absent from the result.</summary>
    Task<List<Organization>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
}

/// <summary>Projection, not an entity — one organization plus how many users belong to it, counted
/// in SQL. Lives here rather than being the Business-layer OrganizationResponse for the same reason
/// SearchAsync takes a BaseSearchObject: Data cannot reference Business.</summary>
public record OrganizationWithUserCount(Organization Organization, int UserCount);
