using eTicketing.Contracts.Persistence;

namespace eTicketing.Contracts.Events;

/// <summary>
/// Published once per completed POST /purchases — per ORDER, not per minted Ticket. A single
/// purchase can mint several Tickets (2x Odrasli + 1x Djeca), and the downstream flow needs all
/// of them together: eTicketing.PdfGeneration renders one PDF per ticket but publishes a single
/// <see cref="TicketPdfReady"/> so eTicketing.Notifications can send ONE confirmation email
/// carrying every PDF as an attachment.
/// </summary>
/// <param name="ProductName">Read from Ticketing's own ProductSnapshot at purchase time, so
/// eTicketing.PdfGeneration needs no client of its own to put a real event name on the ticket. The
/// value is deliberately as-of-purchase: a ticket should show what was bought, not what the product
/// was later renamed to.
///
/// <para>Nullable for one reason only — a <see cref="TicketPurchased"/> published by the previous
/// deployment is still in flight and deserializes these three as null. Consumers must render
/// something sensible rather than treat it as a poison message; they must not start *depending* on
/// null.</para></param>
public record TicketPurchased(
    Guid OrderId,
    Guid ProductId,
    Guid SectorId,
    string SectorName,
    TicketingMode TicketingMode,
    Guid UserId,
    string UserEmail,
    decimal TotalPaid,
    DateTime PurchasedAt,
    IReadOnlyList<PurchasedTicket> Tickets,
    string? ProductName = null,
    DateTime? ProductDate = null,
    City? ProductCity = null);

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
