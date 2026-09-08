using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Business.Tickets;

/// <summary>
/// The single place a <see cref="Ticket"/> becomes a <see cref="TicketResponse"/>. Both
/// PurchaseService (the freshly-minted tickets in a purchase receipt) and TicketService
/// (GET /tickets/mine) go through here, so the two can't drift on QR payload format — which they
/// silently would if each kept its own private mapper, as they did before the QR fields existed.
///
/// Manual, not Mapster: SectorName/TicketTypeName come from navigations, and QrPayload/QrImage are
/// computed rather than copied.
/// </summary>
public sealed class TicketResponseFactory
{
    private readonly TicketQrCodec _qrCodec;

    public TicketResponseFactory(TicketQrCodec qrCodec)
    {
        _qrCodec = qrCodec;
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
            ticket.IsInside);
    }

    /// <summary>Convenience overload for the common case where the navigations are already loaded.</summary>
    public TicketResponse Create(Ticket ticket) =>
        Create(ticket, ticket.Sector?.Name ?? string.Empty, ticket.TicketType?.Name);
}
