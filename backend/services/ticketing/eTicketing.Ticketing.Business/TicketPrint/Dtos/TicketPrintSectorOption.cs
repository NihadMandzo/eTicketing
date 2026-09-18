namespace eTicketing.Ticketing.Business.TicketPrint;

public sealed record TicketPrintSectorOption(
    Guid SectorId,
    string Name,
    int Capacity,
    int Remaining,
    decimal Price,
    int? PeriodYear,
    int? PeriodMonth,
    IReadOnlyList<TicketPrintTicketTypeOption> TicketTypes);
