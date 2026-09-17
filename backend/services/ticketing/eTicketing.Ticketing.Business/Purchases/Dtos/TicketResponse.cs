using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Business.Purchases;

/// <param name="QrPayload">The signed code this ticket's QR encodes. Shown to the holder as a
/// fallback the gate can type in, and the exact string a scanner reads back.</param>
/// <param name="QrImage">A ready-to-render <c>data:image/png;base64,...</c> QR. Rendered here
/// rather than in each client so web and mobile need no QR library of their own — see
/// TicketQrImage.</param>
public record TicketResponse(
    Guid Id,
    Guid OrderId,
    Guid SectorId,
    string SectorName,
    Guid ProductId,
    Guid? TicketTypeId,
    string? TicketTypeName,
    TicketStatus Status,
    decimal PricePaid,
    DateOnly? ValidDate,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    DateTime CreatedAt,
    string QrPayload,
    string QrImage,
    /// <summary>RecurringReservation only: whether the holder is currently inside, per the gate's
    /// entry/exit toggle (see TicketValidationService). Always false for the one-shot modes, which
    /// are spent by their single scan rather than tracked in and out.</summary>
    bool IsInside = false);
