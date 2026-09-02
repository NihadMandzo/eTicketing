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

/// <summary>Which of the two audiences a <see cref="ProductDeletedNotification"/> is written
/// for. The wording differs materially — a buyer is told their ticket is void and who to chase
/// for their money; an organizer is told the platform removed their product.</summary>
public enum ProductDeletedAudience
{
    Buyer,
    Organizer
}

/// <summary>
/// eTicketing.Ticketing → eTicketing.Notifications. One event per recipient with the address
/// already resolved, so Notifications stays a pure renderer with no clients of its own — same
/// shape and reasoning as <see cref="ProductChangedNotification"/>.
/// </summary>
/// <param name="OrganizerEmail">The organization's own contact address, resolved from Identity.
/// Null when Identity has none on file or was unreachable; the template then omits the contact
/// block rather than printing an empty line.</param>
/// <param name="TicketCount">How many still-valid tickets this recipient held. Buyer emails only
/// (0 for the organizer), so the buyer can tell a one-ticket cancellation from a six-ticket one.</param>
public record ProductDeletedNotification(
    Guid ProductId,
    string ProductName,
    string RecipientEmail,
    ProductDeletedAudience Audience,
    DateTime? ProductDate,
    string OrganizerName,
    string? OrganizerEmail,
    string? OrganizerPhone,
    int TicketCount);
