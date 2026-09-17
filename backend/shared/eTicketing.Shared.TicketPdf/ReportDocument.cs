using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static eTicketing.Shared.TicketPdf.TicketTheme;

namespace eTicketing.Shared.TicketPdf;

/// <summary>
/// Renders one exported report (GET /reports/export) as a landscape A4 PDF.
///
/// It lives in this project rather than in eTicketing.Ticketing because everything it needs is
/// already here: the QuestPDF package reference, the embedded Manrope faces, and
/// <see cref="TicketTheme"/>'s palette — which is the same eKarta green scale the Reports design
/// is drawn in (#1D5B3A / #6FAE4A / #8DC63F on #111827 text and #E5E7EB rules). A second PDF
/// project would have duplicated all three, and the exported report would have drifted away from
/// the printed ticket's typography the first time either changed.
///
/// It knows nothing about tickets, organizations or check-ins — the caller flattens whichever of
/// the four reports it is into <see cref="ReportPdfModel"/>'s tile/chart/table vocabulary first,
/// with every number already formatted in Bosnian. See that type for why.
///
/// The measurements are the design's CSS pixels, converted once by <see cref="TicketTheme.Px"/>,
/// exactly as PrintSheetDocument does.
/// </summary>
public class ReportDocument : IDocument
{
    // The three semantic accents the reports use on top of TicketTheme's brand palette. They are
    // the design's own values (#15803D for money that came in, #EF4444 for money that went back
    // out) and are deliberately declared here rather than added to TicketTheme: a printed ticket
    // has no notion of a good or bad number, and widening the ticket theme with report-only
    // colours would invite them onto the ticket by accident.
    private const string Positive = "#15803D";
    private const string Negative = "#EF4444";

    /// <summary>Tallest a chart bar may be drawn, in design pixels — the fixed plot height the
    /// bars' ratios are multiplied by.</summary>
    private const float ChartHeightPx = 150;

    private readonly ReportPdfModel _model;
    private readonly QuestPDF.Infrastructure.Image _logo;

    public ReportDocument(ReportPdfModel model)
    {
        _model = model;
        // One shared Image rather than raw bytes, for the reason spelled out in
        // PrintSheetDocument's constructor: bytes are re-embedded per use, and the mark repeats on
        // every page's header.
        _logo = QuestPDF.Infrastructure.Image.FromBinaryData(TicketTheme.Logo);
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"eKarta — {_model.Title} — {_model.RangeLabel}",
        Author = "eKarta",
    };

    public void Compose(IDocumentContainer container)
    {
        EnsureFontsRegistered();

        container.Page(page =>
        {
            // Landscape: the Učinak Proizvoda and Organizacije tables are six and seven columns
            // wide, and portrait A4 squeezes the name column down to something unreadable.
            page.Size(PageSizes.A4.Landscape());
            page.Margin(Px(28));
            page.DefaultTextStyle(x => x.FontFamily(SansRegular).FontSize(Px(10)).FontColor(TextPrimary));

            page.Header().Element(ComposeHeader);
            page.Content().PaddingTop(Px(18)).Element(ComposeBody);
            page.Footer().Element(ComposeFooter);
        });
    }

    // ── Header / footer ──────────────────────────────────────────────────────────────────────

    private void ComposeHeader(IContainer container)
    {
        container.Background(GreenDark).Padding(Px(16)).Row(row =>
        {
            row.AutoItem()
                .Width(Px(46)).Height(Px(46))
                .Background(White).CornerRadius(Px(23))
                .AlignMiddle().AlignCenter()
                .Width(Px(30)).Height(Px(30))
                .Image(_logo).FitArea();

            row.RelativeItem().PaddingLeft(Px(14)).Column(column =>
            {
                column.Item().Text(_model.Title).Style(Title(19, White));
                column.Item().PaddingTop(Px(3)).Text(_model.Scope).Style(Body(10.5f, 1.2f, GreenLight));
            });

            row.AutoItem().AlignMiddle().Column(column =>
            {
                column.Item().AlignRight().Text("PERIOD").Style(Label(8, GreenLight, strong: true));
                column.Item().AlignRight().PaddingTop(Px(3)).Text(_model.RangeLabel).Style(Value(11.5f, White));
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.PaddingTop(Px(10)).Row(row =>
        {
            row.RelativeItem().Text(_model.GeneratedAtLabel).Style(Body(8.5f, 1.2f, TextMuted));
            row.AutoItem().Text(text =>
            {
                text.DefaultTextStyle(Body(8.5f, 1.2f, TextMuted));
                text.Span("Strana ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    // ── Body ─────────────────────────────────────────────────────────────────────────────────

    private void ComposeBody(IContainer container)
    {
        container.Column(column =>
        {
            if (_model.Tiles.Count > 0)
                column.Item().Element(ComposeTiles);

            // Above the chart, not below it: on the AI Uvidi export this paragraph is the point of
            // the page, and a reader who stops after the first block should have read the summary.
            if (!string.IsNullOrWhiteSpace(_model.Summary))
                column.Item().PaddingTop(Px(18)).Element(ComposeSummary);

            if (_model.Chart is { Count: > 0 })
                column.Item().PaddingTop(Px(18)).Element(ComposeChart);

            if (_model.Notes is { Count: > 0 })
                column.Item().PaddingTop(Px(18)).Element(ComposeNotes);

            if (_model.Table is not null)
                column.Item().PaddingTop(Px(18)).Element(ComposeTable);
        });
    }

    /// <summary>The generated lead paragraph. Marked as such in the panel's label rather than
    /// presented as the platform's own words — a reader deciding how much to trust a sentence needs
    /// to know a model wrote it.</summary>
    private void ComposeSummary(IContainer container)
    {
        container
            .Border(Px(1)).BorderColor(GreenLight).CornerRadius(Px(12))
            .Background(StubSurface)
            .Padding(Px(14))
            .Column(column =>
            {
                column.Item().Text("AI SAŽETAK").Style(Label(8, GreenDark, strong: true));
                column.Item().PaddingTop(Px(6)).Text(_model.Summary!).Style(Body(10.5f, 1.45f, TextBody));
            });
    }

    private void ComposeNotes(IContainer container)
    {
        var notes = _model.Notes!;

        container.Element(Card).Column(column =>
        {
            column.Item().PaddingBottom(Px(10))
                .Text(_model.NotesTitle ?? "Nalazi").Style(Title(12));

            foreach (var note in notes)
            {
                column.Item()
                    // A colour bar rather than a tinted background: these blocks stack, and four
                    // filled panels in a row turn the page into a traffic light.
                    .PaddingBottom(Px(10))
                    .BorderLeft(Px(3)).BorderColor(ColorFor(note.Emphasis))
                    .PaddingLeft(Px(10))
                    .Column(noteColumn =>
                    {
                        noteColumn.Item().Text(note.Title).Style(Value(10.5f, TextPrimary));
                        noteColumn.Item().PaddingTop(Px(3)).Text(note.Body).Style(Body(9.5f, 1.35f, TextBody));
                    });
            }
        });
    }

    private void ComposeTiles(IContainer container)
    {
        container.Row(row =>
        {
            foreach (var tile in _model.Tiles)
            {
                row.RelativeItem().PaddingRight(Px(10)).Element(c => ComposeTile(c, tile));
            }
        });
    }

    private void ComposeTile(IContainer container, ReportPdfTile tile)
    {
        container
            .Border(Px(1)).BorderColor(Border).CornerRadius(Px(10))
            .Background(StubSurface)
            .Padding(Px(12))
            .Column(column =>
            {
                column.Item().Text(tile.Label).Style(Body(9, 1.2f, TextMuted));
                column.Item().PaddingTop(Px(4)).Text(tile.Value).Style(Value(17, ColorFor(tile.Emphasis)));

                if (!string.IsNullOrWhiteSpace(tile.Hint))
                    column.Item().PaddingTop(Px(4)).Text(tile.Hint).Style(Body(8.5f, 1.2f, TextMuted));
            });
    }

    private void ComposeChart(IContainer container)
    {
        var bars = _model.Chart!;

        container.Element(Card).Column(column =>
        {
            if (!string.IsNullOrWhiteSpace(_model.ChartTitle))
                column.Item().PaddingBottom(Px(12)).Text(_model.ChartTitle).Style(Title(12));

            column.Item().Height(Px(ChartHeightPx + 32)).Row(row =>
            {
                foreach (var bar in bars)
                {
                    row.RelativeItem().PaddingHorizontal(Px(3)).Column(barColumn =>
                    {
                        barColumn.Item().AlignCenter().Text(bar.Value).Style(Label(8, TextMuted, letterSpacing: 0, strong: true));

                        // The spacer above each bar is what makes them share a baseline: QuestPDF
                        // has no "align to bottom of a fixed plot area", so the bar's own height is
                        // subtracted from the plot height and the remainder is left empty above it.
                        var height = Math.Clamp(bar.Ratio, 0f, 1f) * ChartHeightPx;
                        barColumn.Item().PaddingTop(Px(4)).Height(Px(ChartHeightPx - height));
                        barColumn.Item()
                            .Height(Px(Math.Max(height, 2)))
                            .Background(GreenMid)
                            .CornerRadius(Px(3));

                        barColumn.Item().PaddingTop(Px(5)).AlignCenter()
                            .Text(bar.Label).Style(Body(8, 1.1f, TextMuted));
                    });
                }
            });
        });
    }

    private void ComposeTable(IContainer container)
    {
        var model = _model.Table!;

        container.Element(Card).Column(column =>
        {
            column.Item().PaddingBottom(Px(10)).Text(model.Title).Style(Title(12));

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(definition =>
                {
                    foreach (var col in model.Columns)
                        definition.RelativeColumn(col.Weight);
                });

                // Repeated on every page — a ten-page product table is unreadable if only the
                // first page says which column is which.
                table.Header(header =>
                {
                    foreach (var col in model.Columns)
                    {
                        header.Cell()
                            .BorderBottom(Px(1)).BorderColor(Border)
                            .PaddingBottom(Px(6)).PaddingRight(Px(6))
                            .AlignmentFor(col)
                            .Text(col.Header.ToUpperInvariant()).Style(Label(8, TextMuted, strong: true));
                    }
                });

                foreach (var row in model.Rows)
                {
                    for (var i = 0; i < model.Columns.Count; i++)
                    {
                        var col = model.Columns[i];
                        table.Cell()
                            .BorderBottom(Px(1)).BorderColor(Surface)
                            .PaddingVertical(Px(7)).PaddingRight(Px(6))
                            .AlignmentFor(col)
                            // A row shorter than the header is a caller bug, but it must not throw
                            // mid-render and lose the whole export — the cell is simply blank.
                            .Text(i < row.Count ? row[i] : string.Empty)
                            .Style(Body(9.5f, 1.25f, TextBody));
                    }
                }

                if (model.TotalsRow is not null)
                {
                    for (var i = 0; i < model.Columns.Count; i++)
                    {
                        var col = model.Columns[i];
                        table.Cell()
                            .BorderTop(Px(1)).BorderColor(Border)
                            .PaddingVertical(Px(8)).PaddingRight(Px(6))
                            .AlignmentFor(col)
                            .Text(i < model.TotalsRow.Count ? model.TotalsRow[i] : string.Empty)
                            .Style(Value(10, TextPrimary));
                    }
                }
            });
        });
    }

    // ── Shared bits ──────────────────────────────────────────────────────────────────────────

    /// <summary>The white bordered panel every block on the page sits in — the design's
    /// `background:#fff; border:1px solid #E5E7EB; border-radius:16px` card.</summary>
    private static IContainer Card(IContainer container) => container
        .Border(Px(1)).BorderColor(Border).CornerRadius(Px(12))
        .Background(White).Padding(Px(14));

    private static string ColorFor(ReportPdfEmphasis emphasis) => emphasis switch
    {
        ReportPdfEmphasis.Positive => Positive,
        ReportPdfEmphasis.Negative => Negative,
        _ => TextPrimary
    };
}
