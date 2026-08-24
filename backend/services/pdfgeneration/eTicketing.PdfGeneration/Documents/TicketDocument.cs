using eTicketing.Contracts.Events;
using eTicketing.Contracts.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace eTicketing.PdfGeneration.Documents;

/// <summary>
/// One printable ticket, one page. Rendered once per <see cref="PurchasedTicket"/> in an order, so
/// a buyer of three tickets gets three separate PDFs attached to one email — each is handed to a
/// different person at the gate, which a single stapled document could not be.
///
/// All user-facing text is Bosnian per .claude/rules/00-workflow-and-testing.md. The validity line
/// is mode-aware because the three TicketingModes mean genuinely different things by "when is this
/// good for": a fixed showing time, one chosen calendar day, or a billing period.
/// </summary>
public class TicketDocument : IDocument
{
    private readonly TicketPurchased _order;
    private readonly PurchasedTicket _ticket;
    private readonly string _productName;
    private readonly DateTime? _productDate;
    private readonly string _productCity;
    private readonly byte[] _qrPng;

    public TicketDocument(
        TicketPurchased order, PurchasedTicket ticket, string productName, DateTime? productDate, string productCity, byte[] qrPng)
    {
        _order = order;
        _ticket = ticket;
        _productName = productName;
        _productDate = productDate;
        _productCity = productCity;
        _qrPng = qrPng;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"eKarta — {_productName}",
        Author = "eKarta",
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            // Named explicitly, not left to the default: the Linux container has no fonts at all
            // out of the box. Its Dockerfile installs fonts-liberation, which fontconfig aliases
            // Arial -> Liberation Sans and Courier New -> Liberation Mono — so these two names
            // resolve identically here and on a Windows dev machine, with the Bosnian diacritics
            // (č/ć/ž/š/đ) actually covered rather than rendered as boxes.
            page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Arial).FontColor("#1f2937"));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Background("#1f2937").Padding(16).Column(column =>
        {
            column.Item().Text("eKarta").FontSize(20).Bold().FontColor("#ffffff");
            column.Item().PaddingTop(2).Text(_productName).FontSize(14).FontColor("#e5e7eb");
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(24).Column(column =>
        {
            column.Spacing(18);

            column.Item().AlignCenter().Column(qr =>
            {
                qr.Item().Width(5, Unit.Centimetre).Height(5, Unit.Centimetre).Image(_qrPng);
                qr.Item().PaddingTop(6).AlignCenter().Text("Skenirajte na ulazu").FontSize(9).FontColor("#6b7280");
            });

            column.Item().AlignCenter().Column(code =>
            {
                code.Item().AlignCenter().Text("Kod ulaznice").FontSize(9).FontColor("#6b7280");
                code.Item().AlignCenter().Text(_ticket.TicketId.ToString().ToUpperInvariant())
                    .FontSize(11).Bold().FontFamily(Fonts.CourierNew);
            });

            column.Item().Element(ComposeDetails);
        });
    }

    private void ComposeDetails(IContainer container)
    {
        container.Border(1).BorderColor("#e5e7eb").Padding(16).Column(column =>
        {
            column.Spacing(8);

            Row(column, "Vrijedi", ValidityLine());
            Row(column, "Lokacija", _productCity);
            Row(column, "Sektor", _order.SectorName);

            if (_ticket.TicketTypeName is not null)
                Row(column, "Vrsta ulaznice", _ticket.TicketTypeName);

            Row(column, "Cijena", $"{_ticket.PricePaid:0.00} KM");
            Row(column, "Broj narudžbe", _order.OrderId.ToString().ToUpperInvariant());
            Row(column, "Kupljeno", _order.PurchasedAt.ToString("dd.MM.yyyy. HH:mm"));
        });
    }

    /// <summary>The three modes answer "when is this good for" from different fields entirely, so
    /// there is no single date column to print — see .claude/rules/01-domain.md.</summary>
    private string ValidityLine() => _order.TicketingMode switch
    {
        TicketingMode.DailyEntry => _ticket.ValidDate is { } d
            ? $"{d:dd.MM.yyyy}."
            : "Datum nije određen",

        TicketingMode.RecurringReservation => _ticket.ValidFrom is { } from && _ticket.ValidTo is { } to
            ? $"{from:dd.MM.yyyy}. – {to:dd.MM.yyyy}."
            : "Period nije određen",

        _ => _productDate?.ToString("dd.MM.yyyy. HH:mm") ?? "Datum nije određen",
    };

    private static void Row(ColumnDescriptor column, string label, string value)
    {
        column.Item().Row(row =>
        {
            row.ConstantItem(4, Unit.Centimetre).Text(label).FontColor("#6b7280");
            row.RelativeItem().Text(value).SemiBold();
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.PaddingTop(12).BorderTop(1).BorderColor("#e5e7eb").PaddingTop(8)
            .Text("Ova ulaznica vrijedi za jedan ulaz i poništava se prilikom prvog skeniranja.")
            .FontSize(9).FontColor("#9ca3af");
    }
}
