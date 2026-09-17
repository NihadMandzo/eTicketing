namespace eTicketing.Ticketing.Business.Purchases;

public record PurchaseResponse(
    Guid OrderId,
    Guid ProductId,
    Guid SectorId,
    decimal TotalPaid,
    DateTime PurchasedAt,
    IReadOnlyList<TicketResponse> Tickets);
