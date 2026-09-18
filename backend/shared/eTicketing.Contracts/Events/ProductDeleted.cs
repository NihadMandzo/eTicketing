namespace eTicketing.Contracts.Events;

/// <summary>
/// Published by eTicketing.Catalog when a Product is deleted, whatever its publish status.
/// Consumed by eTicketing.Ticketing, the only service that knows who holds a ticket for it.
///
/// <para>Unlike <see cref="ProductUpdated"/> this fires for drafts too. A draft has no buyers by
/// construction, so it produces no buyer email — but when platform staff delete another
/// organization's product, that organization still has to be told, and a draft they were still
/// preparing is exactly the case where they would otherwise never find out.</para>
/// </summary>
/// <param name="DeletedByPlatformStaff">True when a SuperAdmin or Admin deleted a product that is
/// not theirs to own. Drives the second notification: the organization hears about a deletion it
/// did not perform. An organizer deleting their own product is told nothing — they just did it.</param>
public record ProductDeleted(
    Guid ProductId,
    string ProductName,
    Guid OrganizationId,
    DateTime? ProductDate,
    DateTime DeletedAt,
    bool DeletedByPlatformStaff);
