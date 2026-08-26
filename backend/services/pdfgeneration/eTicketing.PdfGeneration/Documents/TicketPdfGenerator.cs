using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.PdfGeneration.External;
using eTicketing.PdfGeneration.Options;
using eTicketing.Shared.TicketPdf;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace eTicketing.PdfGeneration.Documents;

public interface ITicketPdfGenerator
{
    /// <summary>Renders one PDF per ticket in the order and returns the <see cref="TicketPdfReady"/>
    /// carrying them. Returns null when the order can't be rendered at all (the product no longer
    /// exists), which the caller treats as a poison message.</summary>
    Task<TicketPdfReady?> GenerateAsync(TicketPurchased order, CancellationToken ct = default);
}

/// <summary>
/// The whole of SPRINT_4 US-4.2 in one place: resolve the product, render a page per ticket, and
/// hand back the event that carries them to eTicketing.Notifications.
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
    private readonly ICatalogClient _catalogClient;
    private readonly TicketSupportInfo _support;
    private readonly ILogger<TicketPdfGenerator> _logger;

    public TicketPdfGenerator(
        ICatalogClient catalogClient,
        IOptions<TicketSupportOptions> support,
        ILogger<TicketPdfGenerator> logger)
    {
        _catalogClient = catalogClient;
        _support = new TicketSupportInfo(support.Value.Email, support.Value.Phone);
        _logger = logger;
    }

    public async Task<TicketPdfReady?> GenerateAsync(TicketPurchased order, CancellationToken ct = default)
    {
        var product = await _catalogClient.GetProductAsync(order.ProductId, ct);
        if (product is null)
        {
            // Not retryable: a deleted product never comes back, so re-attempting this forever
            // would just keep one message circulating. The caller dead-letters it.
            _logger.LogError(
                "Proizvod {ProductId} iz narudžbe {OrderId} ne postoji — PDF se ne može generisati.",
                order.ProductId, order.OrderId);
            return null;
        }

        var generated = new List<TicketPdf>(order.Tickets.Count);

        foreach (var ticket in order.Tickets)
        {
            var model = new TicketPdfModel(
                ticket.TicketId,
                order.OrderId,
                ticket.QrPayload,
                product.Name,
                product.Date,
                product.City.ToDisplayName(),
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

        return new TicketPdfReady(
            order.OrderId, order.ProductId, order.UserId, order.UserEmail,
            product.Name, product.Date, product.City.ToDisplayName(), order.TotalPaid, generated);
    }
}
