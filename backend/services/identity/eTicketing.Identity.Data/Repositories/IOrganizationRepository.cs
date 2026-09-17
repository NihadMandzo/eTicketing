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
    /// collection. One organization's staff is a bounded read, unlike a whole page of them, so this
    /// one keeps the Include.</summary>
    Task<Organization?> GetByIdWithUsersAsync(Guid id, CancellationToken ct = default);

    /// <summary>Every organization's contact details plus its OrganizationSuperAdmin's address, in
    /// one query — the periodic republish that keeps eTicketing.Ticketing's OrganizationSnapshot
    /// read model honest.
    ///
    /// <para>A projection, not entities, and specifically not an <c>Include(o =&gt; o.Users)</c>:
    /// this runs over every organization on the platform, and the super admin's email is one value
    /// per row. The correlated sub-select costs a row each; the include would cost every staff
    /// account on the platform.</para></summary>
    Task<List<OrganizationSnapshotRow>> GetSnapshotRowsAsync(CancellationToken ct = default);
}
