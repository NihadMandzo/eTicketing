namespace eTicketing.Contracts.Events;

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
