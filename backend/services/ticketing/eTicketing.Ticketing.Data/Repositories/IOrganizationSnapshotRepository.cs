using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Data.Repositories;

public interface IOrganizationSnapshotRepository : IRepository<OrganizationSnapshot, Guid>
{
    /// <summary>Read-only single lookup — the deleted-product notice resolving one organization's
    /// refund contact.</summary>
    Task<OrganizationSnapshot?> GetByIdNoTrackingAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>The report label lookup: every organization a report has rows for, in one query.
    /// Replaces a batched HTTP call that had to be chunked at 200 ids to stay inside the other
    /// service's cap; a local IN has no such ceiling. Unknown ids are simply absent, exactly as
    /// they were before — a report must not fail because an organization was deleted.</summary>
    Task<List<OrganizationSnapshot>> GetByIdsNoTrackingAsync(IReadOnlyList<Guid> organizationIds, CancellationToken ct = default);
}
