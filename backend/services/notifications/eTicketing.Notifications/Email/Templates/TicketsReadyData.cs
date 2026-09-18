namespace eTicketing.Notifications.Email.Templates;

public sealed record TicketsReadyData(
    string ProductName,
    string ValidityLine,
    string City,
    decimal TotalPaid,
    IReadOnlyList<TicketsReadyLine> Tickets);
