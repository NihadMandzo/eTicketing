using eTicketing.Contracts.Events;

namespace eTicketing.Ticketing.Business.ReadModels;

/// <summary>
/// Keeps Ticketing's OrganizationSnapshot table in step with eTicketing.Identity. Same contract and
/// same reasoning as <see cref="IProductSnapshotProjector"/> — safe to run twice, and an event older
/// than the row it holds is dropped rather than applied.
///
/// <para>There is no <c>EnsureAsync</c> or <c>BackfillAsync</c> here, and that asymmetry is
/// deliberate: Ticketing has no whitelisted synchronous call to Identity to pull missing rows with,
/// so the catch-up is pushed from the other side instead (Identity's
/// OrganizationSnapshotRepublishWorker).</para>
/// </summary>
public interface IOrganizationSnapshotProjector
{
    Task ApplyAsync(OrganizationSnapshotChanged message, CancellationToken ct = default);

    Task RemoveAsync(Guid organizationId, CancellationToken ct = default);
}
