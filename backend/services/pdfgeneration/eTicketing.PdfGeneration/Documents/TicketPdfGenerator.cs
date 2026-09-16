using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.PdfGeneration.Options;
using eTicketing.Shared.TicketPdf;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace eTicketing.PdfGeneration.Documents;

/// <summary>
/// The whole of SPRINT_4 US-4.2 in one place: render a page per ticket and hand back the event that
/// carries them to eTicketing.Notifications.
///
/// No client of its own any more. The product's name, date and city ride on
/// <see cref="TicketPurchased"/>, read from eTicketing.Ticketing's own ProductSnapshot at the moment
/// of purchase — which removed an undocumented synchronous dependency on eTicketing.Catalog, and
/// with it the branch where a product deleted between purchase and render turned a paid-for order
/// into a dead-lettered message. The values are also more correct than a lookup: a ticket should
/// say what was bought, not what the product was renamed to afterwards.
///
/// One PDF per ticket rather than one per order: each is handed to a different person at the gate,
/// which a single stapled document could not be.
///
/// Nothing is persisted. A ticket PDF is a pure function of the ticket — the QR payload is
/// deterministic for a given ticket id and signing key — so the sheet is rendered here for the
/// e-mail and re-rendered by eTicketing.Ticketing when a buyer downloads it, rather than a copy of
/// a gate-opening QR code being left sitting in blob storage.
/// </summary>
public class TicketPdfGenerator : ITicketPdfGenerator
{
    /// <summary>What goes on the sheet when the event carries no product name — only possible for
    /// a <see cref="TicketPurchased"/> published by the deployment before these fields existed, and
    /// still in flight. A generic heading beats dead-lettering somebody's paid-for ticket.</summary>
    private const string UnknownProductName = "Ulaznica";

    private readonly TicketSupportInfo _support;
    private readonly ILogger<TicketPdfGenerator> _logger;

    public TicketPdfGenerator(
        IOptions<TicketSupportOptions> support,
        ILogger<TicketPdfGenerator> logger)
    {
        _support = new TicketSupportInfo(support.Value.Email, support.Value.Phone);
        _logger = logger;
    }

    public Task<TicketPdfReady> GenerateAsync(TicketPurchased order, CancellationToken ct = default)
    {
        var productName = string.IsNullOrWhiteSpace(order.ProductName) ? UnknownProductName : order.ProductName;
        var productCity = order.ProductCity?.ToDisplayName() ?? string.Empty;

        if (order.ProductName is null)
        {
            _logger.LogWarning(
                "Narudžba {OrderId} ne nosi podatke o proizvodu — vjerovatno je objavljena prije nadogradnje; koristim generički naziv.",
                order.OrderId);
        }

        var generated = new List<TicketPdf>(order.Tickets.Count);

        foreach (var ticket in order.Tickets)
        {
            var model = new TicketPdfModel(
                ticket.TicketId,
                order.OrderId,
                ticket.QrPayload,
                productName,
                order.ProductDate,
                productCity,
                order.SectorName,
                ticket.TicketTypeName,
                ticket.PricePaid,
                order.TicketingMode,
                ticket.ValidDate,
                ticket.ValidFrom,
                ticket.ValidTo,
                order.PurchasedAt,
                order.UserEmail);

            generated.Add(new TicketPdf(
                ticket.TicketId,
                new TicketDocument(model, _support).GeneratePdf(),
                model.FileName,
                order.SectorName,
                ticket.TicketTypeName,
                ticket.PricePaid));
        }

        _logger.LogInformation(
            "Generisano {Count} PDF ulaznica za narudžbu {OrderId}.", generated.Count, order.OrderId);

        return Task.FromResult(new TicketPdfReady(
            order.OrderId, order.ProductId, order.UserId, order.UserEmail,
            productName, order.ProductDate, productCity, order.TotalPaid, generated));
    }
}
