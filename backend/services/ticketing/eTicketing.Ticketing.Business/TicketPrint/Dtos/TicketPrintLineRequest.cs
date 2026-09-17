namespace eTicketing.Ticketing.Business.TicketPrint;

/// <summary>One request line: how many tickets of one price tier in one sector. A sector with no
/// TicketTypes is addressed with a null <see cref="TicketTypeId"/> and priced at Sector.Price,
/// mirroring how PurchaseService treats the same case.</summary>
public sealed record TicketPrintLineRequest
{
    public Guid SectorId { get; init; }
    public Guid? TicketTypeId { get; init; }
    public int Quantity { get; init; }
}
