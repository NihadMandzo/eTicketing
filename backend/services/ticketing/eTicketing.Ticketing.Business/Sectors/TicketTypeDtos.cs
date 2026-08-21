namespace eTicketing.Ticketing.Business.Sectors;

public record TicketTypeResponse(Guid Id, Guid SectorId, string Name, decimal Price, DateTime CreatedAt);

public record UpsertTicketTypeRequest
{
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
}
