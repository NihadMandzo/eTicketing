namespace eTicketing.Contracts.Events;

/// <summary>
/// eTicketing.Identity → eTicketing.Ticketing. An organization's current contact details, published
/// on every change that can move any of them. Consumed into Ticketing's OrganizationSnapshot read
/// model, which replaced the synchronous Ticketing→Identity call that the Izvještaji reports and the
/// deleted-product notice used to make.
///
/// <para>Unlike the product side there is no whitelisted synchronous call to fall back on, so this
/// is push-only: Identity republishes every organization on startup and periodically, which is both
/// the backfill and the self-healing mechanism. See OrganizationSnapshotRepublishWorker.</para>
/// </summary>
/// <param name="SuperAdminEmail">The organization's OrganizationSuperAdmin, if it has one. Derived
/// from the Users table rather than being a column on Organization, which is exactly why this event
/// also fires on user changes — a super admin editing their own address would otherwise silently rot
/// the refund contact printed on every cancellation email.</param>
/// <param name="ChangedAt">When Identity produced this state. At-least-once delivery with no
/// ordering guarantee across a redelivery, so the projection drops anything older than the row it
/// already holds.</param>
public record OrganizationSnapshotChanged(
    Guid OrganizationId,
    string Name,
    string Address,
    string Email,
    string PhoneNumber,
    string? SuperAdminEmail,
    bool IsActive,
    DateTime ChangedAt);
