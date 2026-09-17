using eTicketing.Identity.Data.Entities;

namespace eTicketing.Identity.Business.Organizations;

/// <summary>
/// Publishes an organization's outward-facing state for eTicketing.Ticketing's OrganizationSnapshot
/// read model, which replaced the synchronous Ticketing→Identity call.
///
/// <para>Nothing here saves. Every publish is an outbox row that joins the caller's own
/// <c>SaveChangesAsync</c>, so an organization edit that fails to commit does not announce itself —
/// see eTicketing.Shared.Messaging.OutboxEventPublisher. <see cref="RepublishAllAsync"/> is the one
/// exception, because it has no caller transaction to join.</para>
///
/// <para>The reason this is a class of its own rather than three lines inside OrganizationService:
/// <c>SuperAdminEmail</c> is derived from the Users table, not a column, so the snapshot has to be
/// republished from the user-management methods too — and getting that derivation subtly different
/// in four places is exactly how a refund contact goes stale without anybody noticing.</para>
/// </summary>
public interface IOrganizationSnapshotPublisher
{
    /// <summary>Publishes the organization's current state, resolving its OrganizationSuperAdmin's
    /// address itself.</summary>
    Task PublishAsync(Organization organization, CancellationToken ct = default);

    /// <summary>Same, for the create path — the founding admin is in the change tracker but not yet
    /// in the database, so the caller supplies the address rather than a query returning null.
    /// </summary>
    Task PublishAsync(Organization organization, string? superAdminEmail, CancellationToken ct = default);

    /// <summary>The organization is gone; Ticketing drops its snapshot row. Its sales stay in the
    /// reports, labelled "Nepoznata organizacija".</summary>
    Task PublishDeletedAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Republishes every organization, and saves. This is both the backfill for a Ticketing
    /// database that has never seen these events and the ongoing self-heal for anything lost while
    /// its consumer was down.
    ///
    /// <para>It is pushed from here rather than pulled from there for one concrete reason: the
    /// product read model can lean on the whitelisted Ticketing→Catalog call to fill its own gaps,
    /// and there is no equivalent whitelisted call to Identity — removing that call is half of what
    /// this phase was for. Returns how many organizations it published.</para>
    /// </summary>
    Task<int> RepublishAllAsync(CancellationToken ct = default);
}
