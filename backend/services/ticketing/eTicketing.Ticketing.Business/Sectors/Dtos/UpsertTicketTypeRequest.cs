namespace eTicketing.Ticketing.Business.Sectors;

public record UpsertTicketTypeRequest
{
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
}
