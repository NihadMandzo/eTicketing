using eTicketing.Shared.Storage;
using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Business.Tickets;

/// <summary>
/// The single place a <see cref="Ticket"/> becomes a <see cref="TicketResponse"/>. Both
/// PurchaseService (the freshly-minted tickets in a purchase receipt) and TicketService
/// (GET /tickets/mine) go through here, so the two can't drift on QR payload format or PDF URL
/// construction — which they silently would if each kept its own private mapper, as they did
/// before the QR/PDF fields existed.
///
/// Manual, not Mapster: SectorName/TicketTypeName come from navigations, and QrPayload/QrImage/PdfUrl
/// are all computed rather than copied.
/// </summary>
public sealed class TicketResponseFactory
{
    /// <summary>Container the PDF worker uploads into — must match PdfGeneration's own constant.</summary>
    public const string PdfContainerName = "ticket-pdfs";

    private readonly TicketQrCodec _qrCodec;
    private readonly IBlobStorageService _blobStorage;

    public TicketResponseFactory(TicketQrCodec qrCodec, IBlobStorageService blobStorage)
    {
        _qrCodec = qrCodec;
        _blobStorage = blobStorage;
    }

    public TicketResponse Create(Ticket ticket, string sectorName, string? ticketTypeName)
    {
        var payload = _qrCodec.Sign(ticket.Id);

        return new TicketResponse(
            ticket.Id, ticket.OrderId, ticket.SectorId, sectorName, ticket.ProductId,
            ticket.TicketTypeId, ticketTypeName,
            ticket.Status, ticket.PricePaid, ticket.ValidDate, ticket.ValidFrom, ticket.ValidTo, ticket.CreatedAt,
            payload,
            TicketQrImage.ToDataUri(payload),
            // GetPublicUrl is pure string construction, no network call — safe to run per row of a
            // paged list (same reasoning as CategoryService.BuildIconUrl).
            ticket.PdfBlobName is null ? null : _blobStorage.GetPublicUrl(PdfContainerName, ticket.PdfBlobName));
    }

    /// <summary>Convenience overload for the common case where the navigations are already loaded.</summary>
    public TicketResponse Create(Ticket ticket) =>
        Create(ticket, ticket.Sector?.Name ?? string.Empty, ticket.TicketType?.Name);
}
