using eTicketing.Contracts.Persistence;

namespace eTicketing.Contracts.Events;

/// <summary>
/// eTicketing.Catalog → eTicketing.Ticketing. The current state of a Product, published on every
/// change to it: creation, publication, and every edit. Consumed into Ticketing's ProductSnapshot
/// read model, which is what lets a sector listing, a capacity hold and a purchase all ask "is this
/// product still published?" without a synchronous hop to Catalog on the anonymous browse path or
/// the purchase critical path.
///
/// <para>Deliberately NOT <see cref="ProductUpdated"/>, which looks superficially similar.
/// <see cref="ProductUpdated"/> is notification-shaped: it fires only for already-published
/// products, only when a diff was detected, and carries Bosnian display strings for an email. This
/// one is state-shaped — it fires unconditionally and carries typed values, because a projection
/// that only hears about *some* changes is a projection that silently rots.</para>
///
/// <para>The matching delete is <see cref="ProductDeleted"/>, which already exists and already
/// reaches Ticketing; it hard-deletes the snapshot row (no soft delete anywhere in this system).
/// </para>
/// </summary>
/// <param name="ChangedAt">When Catalog produced this state. Deliveries are at-least-once and
/// unordered across a redelivery, so the projection compares this against the row it already holds
/// and ignores anything older — otherwise a retried "draft" could overwrite a newer "published".
/// </param>
public record ProductSnapshotChanged(
    Guid ProductId,
    Guid OrganizationId,
    string Name,
    DateTime? Date,
    City City,
    PublishStatus Status,
    TicketingMode TicketingMode,
    DateTime ChangedAt);
