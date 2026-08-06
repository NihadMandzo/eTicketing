namespace eTicketing.Contracts.Events;

public record TicketPurchased(
    int TicketId,
    int EventId,
    int SectorId,
    Guid UserId,
    string UserEmail,
    DateTime PurchasedAt);
