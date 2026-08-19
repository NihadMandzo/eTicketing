namespace eTicketing.Contracts.Events;

public record TicketPurchased(
    Guid TicketId,
    Guid ProductId,
    Guid SectorId,
    Guid UserId,
    string UserEmail,
    DateTime PurchasedAt,
    DateOnly? ValidDate,   // DailyEntry: which calendar day this ticket admits entry for
    DateOnly? ValidFrom,   // RecurringReservation: billing period start
    DateOnly? ValidTo);    // RecurringReservation: billing period end
