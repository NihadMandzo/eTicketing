using eTicketing.Contracts.Persistence;

namespace eTicketing.Shared.TicketPdf;

/// <summary>
/// Everything the printed ticket shows, in one shape both callers can produce.
///
/// Deliberately not the <c>TicketPurchased</c> event: eTicketing.PdfGeneration renders from that
/// event, but eTicketing.Ticketing renders the same sheet on demand from a <c>Ticket</c> row it
/// loaded out of its own database, and has no event to hand. A neutral model keeps the document
/// from depending on either.
/// </summary>
/// <param name="QrPayload">The signed code minted by Ticketing's TicketQrCodec. Deterministic for a
/// given ticket id and signing key, which is what makes on-demand regeneration produce a sheet
/// whose QR is byte-identical to the one that was e-mailed.</param>
public sealed record TicketPdfModel(
    Guid TicketId,
    Guid OrderId,
    string QrPayload,
    string ProductName,
    DateTime? ProductDate,
    string ProductCity,
    string SectorName,
    string? TicketTypeName,
    decimal PricePaid,
    TicketingMode TicketingMode,
    DateOnly? ValidDate,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    DateTime PurchasedAt,
    string BuyerEmail)
{
    /// <summary>Short, readable code — matches the ticket rows in the confirmation email and the
    /// attachment's own filename, so a row and the sheet it refers to are visibly the same ticket.</summary>
    public string ShortSerial => TicketId.ToString("N")[..8].ToUpperInvariant();

    /// <summary>Filename used both for the e-mail attachment and the browser download.</summary>
    public string FileName => $"ulaznica-{ShortSerial}.pdf";
}

/// <summary>Where a buyer turns when something is wrong with their ticket. Configured per
/// deployment rather than hardcoded, and the phone is optional — a deployment with no support line
/// prints the address alone instead of an invented number.</summary>
public sealed record TicketSupportInfo(string Email, string? Phone)
{
    public string Display => string.IsNullOrWhiteSpace(Phone) ? Email : $"{Email} · {Phone}";
}
