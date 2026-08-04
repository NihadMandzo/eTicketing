namespace eTicketing.Contracts.Events;

public record TicketPurchased(
    int TicketId,
    int EventId,
    int SectorId,
    int UserId,
    string UserEmail,
    DateTime PurchasedAt);
