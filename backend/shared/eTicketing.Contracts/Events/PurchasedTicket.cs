namespace eTicketing.Contracts.Events;

/// <summary>One admission unit within a <see cref="TicketPurchased"/> order.</summary>
/// <param name="QrPayload">The signed code this ticket's QR encodes, minted by Ticketing's
/// TicketQrCodec. Carried on the event so eTicketing.PdfGeneration never needs the HMAC signing
/// key — it only renders the string it's handed into a QR image.</param>
public record PurchasedTicket(
    Guid TicketId,
    string QrPayload,
    string? TicketTypeName,
    decimal PricePaid,
    DateOnly? ValidDate,   // DailyEntry: which calendar day this ticket admits entry for
    DateOnly? ValidFrom,   // RecurringReservation: billing period start
    DateOnly? ValidTo);    // RecurringReservation: billing period end
