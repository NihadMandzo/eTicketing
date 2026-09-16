using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using eTicketing.PdfGeneration.Options;
using eTicketing.Shared.TicketPdf;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace eTicketing.PdfGeneration.Documents;

public interface ITicketPdfGenerator
{
    /// <summary>Renders one PDF per ticket in the order and returns the <see cref="TicketPdfReady"/>
    /// carrying them. Never fails for want of product data: everything the sheet needs now arrives
    /// on the event itself.</summary>
    Task<TicketPdfReady> GenerateAsync(TicketPurchased order, CancellationToken ct = default);
}
