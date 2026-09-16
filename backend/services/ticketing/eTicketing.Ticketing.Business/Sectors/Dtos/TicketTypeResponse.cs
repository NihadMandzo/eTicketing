namespace eTicketing.Ticketing.Business.Sectors;

public record TicketTypeResponse(Guid Id, Guid SectorId, string Name, decimal Price, DateTime CreatedAt);
