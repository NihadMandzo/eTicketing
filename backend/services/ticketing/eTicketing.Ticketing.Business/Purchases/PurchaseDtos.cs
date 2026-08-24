using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Business.Purchases;

public record PurchaseRequest
{
    public string HoldId { get; init; } = string.Empty;
    public IReadOnlyList<PurchaseLineItemRequest> LineItems { get; init; } = [];

    // Cosmetic only — eTicketing.Payment is a deterministic mock, never real card processing. Only
    // the last 4 digits of CardNumber ever leave this service (see PurchaseService.Last4).
    public string CardNumber { get; init; } = string.Empty;
    public string CardExpiry { get; init; } = string.Empty;
    public string CardCvv { get; init; } = string.Empty;
}

public record PurchaseLineItemRequest
{
    // Null iff the held Sector has no TicketTypes (today's single-implicit-price path).
    public Guid? TicketTypeId { get; init; }
    public int Quantity { get; init; }
}

public record PurchaseResponse(
    Guid OrderId,
    Guid ProductId,
    Guid SectorId,
    decimal TotalPaid,
    DateTime PurchasedAt,
    IReadOnlyList<TicketResponse> Tickets);

/// <param name="QrPayload">The signed code this ticket's QR encodes. Shown to the holder as a
/// fallback the gate can type in, and the exact string a scanner reads back.</param>
/// <param name="QrImage">A ready-to-render <c>data:image/png;base64,...</c> QR. Rendered here
/// rather than in each client so web and mobile need no QR library of their own — see
/// TicketQrImage.</param>
/// <param name="PdfUrl">Null until eTicketing.PdfGeneration finishes and its TicketPdfReady event
/// stamps Ticket.PdfBlobName (at which point Status is also Ready).</param>
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
    string? PdfUrl);
