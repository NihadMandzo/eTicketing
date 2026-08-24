using eTicketing.Contracts.Events;
using eTicketing.PdfGeneration.External;
using eTicketing.PdfGeneration.Qr;
using eTicketing.Shared.Storage;
using QuestPDF.Fluent;

namespace eTicketing.PdfGeneration.Documents;

public interface ITicketPdfGenerator
{
    /// <summary>Renders and uploads one PDF per ticket in the order, and returns the
    /// <see cref="TicketPdfReady"/> describing them. Returns null when the order can't be rendered
    /// at all (the product no longer exists), which the caller treats as a poison message.</summary>
    Task<TicketPdfReady?> GenerateAsync(TicketPurchased order, CancellationToken ct = default);
}

/// <summary>
/// The whole of SPRINT_4 US-4.2 in one place: resolve the product, draw a QR, render a page,
/// upload it, and hand back the event that tells the rest of the system it's done.
///
/// One PDF per ticket rather than one per order: each is handed to a different person at the gate,
/// which a single stapled document could not be. They're uploaded under
/// <c>{orderId}/{ticketId}.pdf</c> so an order's files stay together in the container.
/// </summary>
public class TicketPdfGenerator : ITicketPdfGenerator
{
    /// <summary>Must match eTicketing.Ticketing's TicketResponseFactory.PdfContainerName — that's
    /// the side that turns the stored blob name back into a download URL.</summary>
    public const string ContainerName = "ticket-pdfs";

    private readonly ICatalogClient _catalogClient;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<TicketPdfGenerator> _logger;

    public TicketPdfGenerator(ICatalogClient catalogClient, IBlobStorageService blobStorage, ILogger<TicketPdfGenerator> logger)
    {
        _catalogClient = catalogClient;
        _blobStorage = blobStorage;
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
            var qrPng = QrCodeRenderer.Render(ticket.QrPayload);
            var document = new TicketDocument(order, ticket, product.Name, product.Date, product.City.ToString(), qrPng);

            var pdfBytes = document.GeneratePdf();

            var blobName = $"{order.OrderId:N}/{ticket.TicketId:N}.pdf";
            using var stream = new MemoryStream(pdfBytes);
            // Upload returns the blob's public URL; nothing needs it. Notifications reads the PDF
            // back by BlobName to attach the bytes, and Ticketing derives TicketResponse.PdfUrl
            // from the same name via GetPublicUrl.
            await _blobStorage.UploadAsync(ContainerName, blobName, stream, "application/pdf", ct);

            generated.Add(new TicketPdf(
                ticket.TicketId,
                blobName,
                // Short id in the filename: three attachments called "ulaznica.pdf" are
                // indistinguishable in a mail client, and the full GUID is unreadable.
                $"ulaznica-{ticket.TicketId.ToString("N")[..8].ToUpperInvariant()}.pdf",
                order.SectorName,
                ticket.TicketTypeName,
                ticket.PricePaid));
        }

        _logger.LogInformation(
            "Generisano {Count} PDF ulaznica za narudžbu {OrderId}.", generated.Count, order.OrderId);

        return new TicketPdfReady(
            order.OrderId, order.ProductId, order.UserId, order.UserEmail,
            product.Name, product.Date, product.City.ToString(), order.TotalPaid, generated);
    }
}
