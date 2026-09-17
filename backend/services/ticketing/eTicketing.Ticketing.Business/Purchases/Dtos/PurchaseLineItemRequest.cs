namespace eTicketing.Ticketing.Business.Purchases;

public record PurchaseLineItemRequest
{
    // Null iff the held Sector has no TicketTypes (today's single-implicit-price path).
    public Guid? TicketTypeId { get; init; }
    public int Quantity { get; init; }
}
