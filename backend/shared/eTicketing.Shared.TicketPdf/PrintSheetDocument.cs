using System.Globalization;
using eTicketing.Contracts.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static eTicketing.Shared.TicketPdf.TicketTheme;

namespace eTicketing.Shared.TicketPdf;

/// <summary>
/// A batch of physical tickets laid out three to an A4 sheet, ready to be guillotined apart. This
/// is the counterpart to <see cref="TicketDocument"/>, which renders a single ticket as a folded
/// three-panel sheet for e-mail: that shape makes no sense in a box-office context, where the point
/// is to get as many sellable tickets per sheet of paper as the design allows without shrinking the
/// QR code below what a gate scanner reads reliably.
///
/// Each ticket occupies exactly a third of the page so the cut lines fall at 99mm and 198mm, and a
/// page break is forced after every third ticket so the page count is always ceil(n / 3) — no
/// half-printed ticket ever straddles a sheet boundary.
///
/// The layout follows the supplied HTML/CSS design; measurements are given in that design's CSS
/// pixels and converted by <see cref="TicketTheme.Px"/>. All user-facing text is Bosnian per
/// .claude/rules/00-workflow-and-testing.md — the design's English labels are a mock-up convention,
/// not a spec.
/// </summary>
public class PrintSheetDocument : IDocument
{
    public const int TicketsPerSheet = 3;

    /// <summary>
    /// A third of the page, in points, derived from the page size rather than written as 99mm.
    ///
    /// The sub-point subtraction matters: three slots of exactly A4.Height / 3 sum to the page
    /// height precisely, and any rounding in the other direction drops the third ticket onto a page
    /// of its own — which would silently double the page count and waste half the paper. Giving up
    /// a quarter of a point per slot (0.09mm across the whole sheet, far below what a guillotine
    /// can register) removes that cliff entirely.
    /// </summary>
    private static readonly float SlotHeight = (PageSizes.A4.Height - 0.75f) / TicketsPerSheet;

    /// <summary>Height reserved for the dashed cut guide under a ticket.</summary>
    private static readonly float CutRuleHeight = Px(2);

    private readonly PrintSheetModel _model;
    private readonly IReadOnlyList<string> _qrCodes;
    private readonly QuestPDF.Infrastructure.Image _logo;

    public PrintSheetDocument(PrintSheetModel model)
    {
        _model = model;
        // Rendered up front, in the same order as the tickets, so Compose stays pure layout. These
        // are SVG rather than PNG — see QrCodeRenderer.RenderSvg for why that is what makes a
        // 5000-ticket batch a viable size at all.
        _qrCodes = [.. model.Tickets.Select(t => QrCodeRenderer.RenderSvg(t.QrPayload))];

        // One shared Image object for the mark that appears on every single ticket, rather than
        // handing the raw bytes to each .Image() call.
        //
        // This is the difference between a usable batch and an unusable one. Passing byte[] makes
        // QuestPDF treat every use as a distinct image and embed a separate copy: the logo is a
        // 204 KB PNG, and it cost ~44 KB per ticket in the finished PDF, so a 5000-ticket export
        // came out around 220 MB — far too big to hold in memory, store, and stream back through
        // the gateway before something timed out. A shared Image is embedded once and referenced
        // from every page.
        //
        // Per-document rather than a static: TicketDocument renders e-mail PDFs concurrently in
        // PdfGeneration, and a shared mutable QuestPDF object across threads is not worth the risk
        // for what is a one-off allocation per document.
        _logo = QuestPDF.Infrastructure.Image.FromBinaryData(TicketTheme.Logo);
    }

    public int PageCount => (int)Math.Ceiling(_model.Tickets.Count / (double)TicketsPerSheet);

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"eKarta — ulaznice za štampu — {_model.ProductName}",
        Author = "eKarta",
    };

    public void Compose(IDocumentContainer container)
    {
        EnsureFontsRegistered();

        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            // No page margin: the header band and the QR stub bleed to the sheet edge, and every
            // millimetre of margin is a millimetre the tickets lose.
            page.Margin(0);
            page.DefaultTextStyle(x => x.FontFamily(SansRegular).FontSize(Px(11)).FontColor(TextPrimary));

            page.Content().Column(column =>
            {
                var sheets = _model.Tickets
                    .Select((ticket, index) => (ticket, index))
                    .GroupBy(x => x.index / TicketsPerSheet)
                    .ToList();

                foreach (var sheet in sheets)
                {
                    var slots = sheet.ToList();

                    foreach (var (ticket, index) in slots)
                    {
                        // The last ticket on a sheet needs no cut guide — the paper edge already is
                        // one. The other two carry the dashed rule the guillotine follows.
                        var isLastOnSheet = ReferenceEquals(slots[^1].ticket, ticket);
                        column.Item().Height(SlotHeight).Element(c => ComposeSlot(c, ticket, _qrCodes[index], !isLastOnSheet));
                    }

                    // Forced rather than left to overflow: this is what guarantees exactly three
                    // tickets per sheet regardless of how the slot heights round.
                    if (!ReferenceEquals(sheet, sheets[^1]))
                    {
                        column.Item().PageBreak();
                    }
                }
            });
        });
    }

    private void ComposeSlot(IContainer container, PrintTicketModel ticket, string qrSvg, bool withCutRule)
    {
        container.Column(column =>
        {
            var bodyHeight = withCutRule ? SlotHeight - CutRuleHeight : SlotHeight;

            column.Item().Height(bodyHeight).Element(c => ComposeTicket(c, ticket, qrSvg));

            if (withCutRule)
            {
                column.Item().Height(CutRuleHeight).AlignMiddle()
                    .LineHorizontal(1).LineColor(BorderStrong).LineDashPattern([Px(4), Px(4)]);
            }
        });
    }

    private void ComposeTicket(IContainer container, PrintTicketModel ticket, string qrSvg)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(c => ComposeTicketBody(c, ticket));
            // Borders in QuestPDF are always solid, so the design's dashed tear-off rule between
            // the ticket and its stub is drawn as a line element between the two columns.
            row.AutoItem().LineVertical(Px(2)).LineColor(Border).LineDashPattern([Px(4), Px(4)]);
            row.ConstantItem(62, Unit.Millimetre).Element(c => ComposeQrStub(c, ticket, qrSvg));
        });
    }

    private void ComposeTicketBody(IContainer container, PrintTicketModel ticket)
    {
        container.Column(column =>
        {
            column.Item().Element(ComposeBrandBar);
            column.Item().PaddingHorizontal(Px(22)).PaddingTop(Px(16)).Element(ComposeEventTitle);
            column.Item().PaddingHorizontal(Px(22)).PaddingTop(Px(14)).Element(c => ComposeFacts(c, ticket));
            column.Item().PaddingTop(Px(6)).Extend().AlignBottom()
                .PaddingHorizontal(Px(22)).PaddingBottom(Px(14)).Element(ComposeDisclaimer);
        });
    }

    private void ComposeBrandBar(IContainer container)
    {
        container.Background(GreenDark).PaddingVertical(Px(8)).PaddingHorizontal(Px(22)).Row(row =>
        {
            row.AutoItem()
                .Width(Px(64)).Height(Px(64))
                .Background(White)
                // A circle in QuestPDF is a fully-rounded square; the roundel is what the design
                // uses to lift the mark off the dark band.
                .CornerRadius(Px(32))
                .AlignMiddle().AlignCenter()
                .Width(Px(41)).Height(Px(42))
                // _logo, not the raw bytes — see the constructor for why that distinction is worth
                // ~44 KB on every ticket in the batch.
                .Image(_logo).FitArea();

            row.RelativeItem().AlignMiddle().AlignRight()
                .Text("ULAZNICA · EKARTA")
                .Style(Label(10, GreenLight, letterSpacing: 0.16f));
        });
    }

    private void ComposeEventTitle(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("DOGAĐAJ").Style(Label(10, letterSpacing: 0.16f));
            // Product names are validated up to 200 characters; at this size that is several lines,
            // which would burst a fixed-height slot and push the ticket onto a sheet of its own.
            column.Item().PaddingTop(Px(4)).Text(_model.ProductName)
                .ClampLines(2, "…")
                .Style(Title(25).LetterSpacing(-0.02f).LineHeight(1.12f));
        });
    }

    /// <summary>The design's 3-column fact grid (1fr / 1.6fr / 0.75fr) — Sector gets the widest
    /// column because it carries the sector name plus an optional ticket-type suffix.</summary>
    private void ComposeFacts(IContainer container, PrintTicketModel ticket)
    {
        container.Column(column =>
        {
            column.Spacing(Px(12));

            column.Item().Row(row =>
            {
                row.Spacing(Px(18));
                row.RelativeItem(1f).Element(c => Fact(c, "LOKACIJA", _model.ProductCity));
                row.RelativeItem(1.6f).Element(c => Fact(c, "SEKTOR", SectorLine(ticket)));
                row.RelativeItem(0.75f).Element(c => Fact(c, "CIJENA", Money(ticket.Price), GreenDark));
            });

            column.Item().Row(row =>
            {
                row.Spacing(Px(18));
                row.RelativeItem(1f).Element(c =>
                    Fact(c, "DATUM IZDAVANJA", _model.IssuedAt.ToString("dd.MM.yyyy.", CultureInfo.InvariantCulture)));

                row.RelativeItem(2.35f).Column(validity =>
                {
                    validity.Item().Text("VRIJEDI").Style(Label(9.5f));
                    validity.Item().PaddingTop(Px(3)).Row(value =>
                    {
                        value.AutoItem().AlignMiddle().Text(ValidityLine(ticket)).Style(Value(14));
                        value.AutoItem().PaddingLeft(Px(8)).AlignMiddle().Element(ComposeEntryBadge);
                    });
                });
            });
        });
    }

    private void ComposeEntryBadge(IContainer container)
    {
        container.Background(Surface).Border(1).BorderColor(Border)
            .PaddingVertical(Px(3)).PaddingHorizontal(Px(8))
            .Text(EntryBadgeText()).Style(Label(9.5f, GreenDark, letterSpacing: 0.1f, strong: true));
    }

    private void ComposeDisclaimer(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().MaxWidth(106, Unit.Millimetre)
                .Text("Ulaznica važi za jedan ulaz i poništava se u trenutku skeniranja. Nije prenosiva i ne " +
                      "podliježe povratu novca; preprodaja iznad nominalne cijene je zabranjena. Ulazak " +
                      "podrazumijeva pristanak na sigurnosnu provjeru i kućni red objekta. " +
                      $"Kontakt: {_model.Support.Display}.")
                .Style(Body(8.5f, 1.45f, TextMuted));

            // The three brand greens as one bar — the design's closing flourish, and a cheap
            // print-quality check: if it prints muddy, the whole sheet will.
            row.AutoItem().AlignBottom().PaddingLeft(Px(16)).Row(bar =>
            {
                foreach (var color in new[] { GreenDark, GreenMid, GreenLight })
                {
                    bar.ConstantItem(Px(64) / 3f).Height(Px(6)).Background(color);
                }
            });
        });
    }

    private static void ComposeQrStub(IContainer container, PrintTicketModel ticket, string qrSvg)
    {
        container
            .Background(StubSurface)
            .PaddingVertical(Px(14)).PaddingHorizontal(Px(14))
            .AlignMiddle()
            .Column(column =>
            {
                column.Spacing(Px(9));

                column.Item().AlignCenter()
                    .Background(White).Border(1).BorderColor(Border).Padding(Px(7))
                    .Width(Px(124)).Height(Px(124))
                    .Svg(qrSvg).FitArea();

                column.Item().AlignCenter().Column(serial =>
                {
                    serial.Item().AlignCenter().Text("SERIJSKI BROJ").Style(Label(9));
                    serial.Item().PaddingTop(Px(3)).AlignCenter().Text($"#{ticket.StubNumber}")
                        .Style(MonoStyle(11.5f).LetterSpacing(0.02f));
                    // The stub number is for the box office; the full id is what gate staff type
                    // into the scanner's manual fallback when a camera will not read the code.
                    serial.Item().PaddingTop(Px(2)).AlignCenter().Text(ticket.Serial)
                        .Style(MonoStyle(7, TextMuted, strong: false).LetterSpacing(0.02f));
                });

                column.Item().AlignCenter().MaxWidth(50, Unit.Millimetre)
                    .Text("Skenirajte na ulazu. Ne dijelite ovaj kod.")
                    .Style(Body(8.5f, 1.4f, TextMuted));
            });
    }

    /// <summary>Label above value, the sheet's basic unit. Values are clamped so a long sector name
    /// cannot push everything below it out of a fixed-height slot.</summary>
    private static void Fact(IContainer container, string label, string value, string? valueColor = null)
    {
        container.Column(column =>
        {
            column.Item().Text(label).Style(Label(9.5f)).ClampLines(1);
            column.Item().PaddingTop(Px(3)).Text(value).ClampLines(2, "…").Style(Value(14, valueColor ?? TextPrimary));
        });
    }

    /// <summary>Bosnian decimal notation uses a comma, and the design shows "25,00 KM". Formatted
    /// against a fixed culture so the container's locale cannot change what a ticket says.</summary>
    private static string Money(decimal amount) =>
        amount.ToString("#,##0.00", BosnianNumbers) + " KM";

    private static readonly NumberFormatInfo BosnianNumbers = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
    };

    private static string SectorLine(PrintTicketModel ticket) =>
        ticket.TicketTypeName is { } type ? $"{ticket.SectorName} · {type}" : ticket.SectorName;

    /// <summary>Only the two modes the export supports appear here: a SingleOccurrence batch is
    /// good for the product's showing time, a DailyEntry batch for the one calendar day the
    /// organizer chose when requesting it. RecurringReservation is rejected before it ever reaches
    /// the renderer (see TicketPrintService).</summary>
    private string ValidityLine(PrintTicketModel ticket) => _model.TicketingMode switch
    {
        TicketingMode.DailyEntry => ticket.ValidDate is { } d
            ? $"{d:dd.MM.yyyy}."
            : "Datum nije određen",

        _ => _model.ProductDate?.ToString("dd.MM.yyyy. HH:mm", CultureInfo.InvariantCulture) ?? "Datum nije određen",
    };

    private string EntryBadgeText() => _model.TicketingMode switch
    {
        TicketingMode.DailyEntry => "DNEVNI ULAZ",
        _ => "JEDAN ULAZ",
    };
}
