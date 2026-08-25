using System.Globalization;
using eTicketing.Contracts.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static eTicketing.Shared.TicketPdf.TicketTheme;

namespace eTicketing.Shared.TicketPdf;

/// <summary>
/// One printable ticket, one page. Rendered once per ticket in an order, so
/// a buyer of three tickets gets three separate PDFs attached to one email — each is handed to a
/// different person at the gate, which a single stapled document could not be.
///
/// The sheet is a three-fold A4: a detachable ticket with its QR stub, entry information, then
/// terms. Each panel is exactly a third of the page so the printed sheet folds on the marked lines.
/// The layout follows a supplied HTML/CSS design; measurements are given in that design's CSS
/// pixels and converted by <see cref="TicketTheme.Px"/>.
///
/// All user-facing text is Bosnian per .claude/rules/00-workflow-and-testing.md — the design's
/// English labels are a mock-up convention, not a spec. The validity line and the entry badge are
/// mode-aware because the three TicketingModes mean genuinely different things by "when is this
/// good for": a fixed showing time, one chosen calendar day, or a billing period.
/// </summary>
public class TicketDocument : IDocument
{
    /// <summary>A third of A4 (297mm) — where the sheet is meant to be folded.</summary>
    private const float PanelHeightMm = 99f;

    /// <summary>Height of the fold marker (rule plus its caption). Fixed rather than
    /// content-derived so the folds can be placed exactly: the rule is drawn at the marker's top
    /// edge, so panel 1 is a full third and panel 2 is a third minus one marker, putting the two
    /// rules at exactly 99mm and 198mm. Letting the caption size itself would drift the second fold
    /// several millimetres down the page, which shows the moment someone actually folds the sheet.
    ///
    /// Must stay comfortably above the caption's own height: too tight and the caption cannot be
    /// laid out, at which point the whole panel is pushed onto a page of its own instead of
    /// erroring. Manrope's line metrics need roughly Px(15) here, so this keeps a point in hand.</summary>
    private static readonly float FoldMarkerHeight = Px(16);

    private readonly TicketPdfModel _model;
    private readonly byte[] _qrPng;
    private readonly TicketSupportInfo _support;

    public TicketDocument(TicketPdfModel model, TicketSupportInfo support)
    {
        _model = model;
        _support = support;
        _qrPng = QrCodeRenderer.Render(model.QrPayload);
    }

    /// <summary>Full ticket id, uppercased. This is what gate staff type into the scanner's manual
    /// fallback when a camera won't read the code, so it has to be the complete GUID — the short
    /// form below is for cross-referencing panels, not for entry.</summary>
    private string Serial => _model.TicketId.ToString().ToUpperInvariant();

    private string ShortSerial => _model.ShortSerial;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"eKarta — {_model.ProductName}",
        Author = "eKarta",
    };

    public void Compose(IDocumentContainer container)
    {
        EnsureFontsRegistered();

        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            // No page margin: the header band, the QR stub and the support footer all bleed to the
            // sheet edge. Every panel applies its own inner padding instead.
            page.Margin(0);
            page.DefaultTextStyle(x => x.FontFamily(SansRegular).FontSize(Px(11)).FontColor(TextPrimary));

            page.Content().Column(column =>
            {
                // Every height is explicit. An Extend() on the last panel claims the whole page
                // rather than the remainder, which pushes each panel onto a page of its own.
                var thirdOfPage = PanelHeightMm * 72f / 25.4f;

                column.Item().Height(thirdOfPage).Element(ComposeTicketPanel);
                column.Item().Height(FoldMarkerHeight).Element(FoldMarker);
                column.Item().Height(thirdOfPage - FoldMarkerHeight).Element(ComposeEntryPanel);
                column.Item().Height(FoldMarkerHeight).Element(FoldMarker);
                column.Item().Height(thirdOfPage - FoldMarkerHeight).Element(ComposeTermsPanel);
            });
        });
    }

    // ---------------------------------------------------------------------------------------
    // Panel 1 — the ticket itself
    // ---------------------------------------------------------------------------------------

    private void ComposeTicketPanel(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(ComposeTicketBody);
            // Borders in QuestPDF are always solid, so the design's dashed tear-off rule is drawn
            // as a line element sitting between the two columns.
            row.AutoItem().LineVertical(Px(2)).LineColor(Border).LineDashPattern([Px(4), Px(4)]);
            row.ConstantItem(62, Unit.Millimetre).Element(ComposeQrStub);
        });
    }

    private void ComposeTicketBody(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Element(ComposeBrandBar);
            column.Item().PaddingHorizontal(Px(22)).PaddingTop(Px(18)).Element(ComposeEventTitle);
            column.Item().PaddingHorizontal(Px(22)).PaddingTop(Px(16)).Element(ComposeTicketFacts);
            column.Item().PaddingTop(Px(8)).Extend().AlignBottom()
                .PaddingHorizontal(Px(22)).PaddingBottom(Px(16)).Element(ComposeDisclaimer);
        });
    }

    private static void ComposeBrandBar(IContainer container)
    {
        container.Background(GreenDark).PaddingVertical(Px(9)).PaddingHorizontal(Px(22)).Row(row =>
        {
            row.AutoItem()
                .Width(Px(70)).Height(Px(70))
                .Background(White)
                // A circle in QuestPDF is a fully-rounded square; the roundel is what the design
                // uses to lift the mark off the dark band.
                .CornerRadius(Px(35))
                .AlignMiddle().AlignCenter()
                .Width(Px(44)).Height(Px(46))
                .Image(Logo).FitArea();

            row.RelativeItem().AlignMiddle().AlignRight()
                .Text("ELEKTRONSKA ULAZNICA · EKARTA")
                .Style(Label(10, GreenLight, letterSpacing: 0.16f));
        });
    }

    private void ComposeEventTitle(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("DOGAĐAJ").Style(Label(10, letterSpacing: 0.16f));
            // Product names are validated up to 200 characters; at this size that is six lines,
            // which would burst the panel and spill the sheet onto extra pages. Two lines is what
            // the design's slack allows, and the full name is in the email and the app anyway.
            column.Item().PaddingTop(Px(4)).Text(_model.ProductName)
                .ClampLines(2, "…")
                .Style(Title(27).LetterSpacing(-0.02f).LineHeight(1.12f));
        });
    }

    /// <summary>The design's 3-column fact grid (1fr / 1.6fr / 0.75fr) — Sector gets the widest
    /// column because it carries the sector name plus an optional ticket-type suffix.</summary>
    private void ComposeTicketFacts(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(Px(14));

            column.Item().Row(row =>
            {
                row.Spacing(Px(18));
                row.RelativeItem(1f).Element(c => Fact(c, "LOKACIJA", _model.ProductCity));
                row.RelativeItem(1.6f).Element(c => Fact(c, "SEKTOR", SectorLine()));
                row.RelativeItem(0.75f).Element(c => Fact(c, "CIJENA", Money(_model.PricePaid), GreenDark));
            });

            column.Item().Row(row =>
            {
                row.Spacing(Px(18));
                row.RelativeItem(1f).Element(c =>
                    Fact(c, "DATUM KUPOVINE", _model.PurchasedAt.ToString("dd.MM.yyyy. HH:mm")));

                row.RelativeItem(2.35f).Column(validity =>
                {
                    validity.Item().Text("VRIJEDI").Style(Label(9.5f));
                    validity.Item().PaddingTop(Px(3)).Row(value =>
                    {
                        value.AutoItem().AlignMiddle().Text(ValidityLine()).Style(Value(15));
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
                      "podrazumijeva pristanak na sigurnosnu provjeru i kućni red objekta. Potpuni uslovi " +
                      "nalaze se na trećem dijelu ovog lista.")
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

    private void ComposeQrStub(IContainer container)
    {
        container
            .Background(StubSurface)
            .PaddingVertical(Px(16)).PaddingHorizontal(Px(14))
            .AlignMiddle()
            .Column(column =>
            {
                column.Spacing(Px(10));

                column.Item().AlignCenter()
                    .Background(White).Border(1).BorderColor(Border).Padding(Px(8))
                    .Width(Px(132)).Height(Px(132))
                    .Image(_qrPng).FitArea();

                column.Item().AlignCenter().Column(serial =>
                {
                    serial.Item().AlignCenter().Text("SERIJSKI BROJ").Style(Label(9));
                    serial.Item().PaddingTop(Px(3)).AlignCenter().Text(Serial)
                        .Style(MonoStyle(9).LetterSpacing(0.04f));
                });

                column.Item().AlignCenter().MaxWidth(46, Unit.Millimetre)
                    .Text("Skenirajte na ulazu. Ne dijelite ovaj kod.")
                    .Style(Body(8.5f, 1.4f, TextMuted));
            });
    }

    // ---------------------------------------------------------------------------------------
    // Panel 2 — entry information
    // ---------------------------------------------------------------------------------------

    private void ComposeEntryPanel(IContainer container)
    {
        container.PaddingHorizontal(Px(22)).PaddingTop(Px(18)).PaddingBottom(Px(14)).Column(column =>
        {
            column.Item().Element(c => SectionHeading(c, "PODACI O ULASKU", GreenMid, ShortSerial));
            column.Item().PaddingTop(Px(16)).Element(ComposeEntryGrid);
            column.Item().PaddingTop(Px(12)).Extend().AlignBottom().Element(ComposeHowToEnter);
        });
    }

    /// <summary>
    /// The design's fact grid for this panel, filled with fields this platform actually has.
    ///
    /// The design's own Venue / Gate / Doors open cells have no source in the domain — there is no
    /// venue or gate entity — so the row carries ticket type, entry mode, start time and buyer
    /// instead, and everything printed is real data.
    /// </summary>
    private void ComposeEntryGrid(IContainer container)
    {
        container.Row(row =>
        {
            row.Spacing(Px(16));
            row.RelativeItem().Element(c => Fact(c, "TIP ULAZNICE", _model.TicketTypeName ?? "Standardna", size: 13));
            row.RelativeItem().Element(c => Fact(c, "NAČIN ULASKA", EntryModeText(), size: 13));
            row.RelativeItem().Element(c => Fact(c, "POČETAK", StartTimeText(), size: 13));
            row.RelativeItem().Element(c => Fact(c, "KUPAC", _model.BuyerEmail, size: 13));
        });
    }

    private void ComposeHowToEnter(IContainer container)
    {
        container.Row(row =>
        {
            row.Spacing(Px(12));
            row.RelativeItem().Element(c => Step(c, GreenDark, "1 · DOLAZAK",
                "Dođite na ulaz najmanje 30 minuta prije početka. Ulaz se zatvara na početku događaja."));
            row.RelativeItem().Element(c => Step(c, GreenMid, "2 · SKENIRANJE",
                "Pokažite QR kod na ekranu ili odštampan. Osvjetljenje ekrana postavite na maksimum."));
            row.RelativeItem().Element(c => Step(c, GreenLight, "3 · ČUVANJE",
                "Sačuvajte ulaznicu do izlaska. Osoblje je može ponovo zatražiti unutar objekta."));
        });
    }

    private static void Step(IContainer container, string accent, string title, string body)
    {
        container.Background(Surface).BorderLeft(Px(3)).BorderColor(accent)
            .PaddingVertical(Px(10)).PaddingHorizontal(Px(12))
            .Column(column =>
            {
                column.Item().Text(title).Style(Label(9, GreenDark, strong: true));
                column.Item().PaddingTop(Px(4)).Text(body).Style(Body(10.5f, 1.45f));
            });
    }

    // ---------------------------------------------------------------------------------------
    // Panel 3 — terms
    // ---------------------------------------------------------------------------------------

    private void ComposeTermsPanel(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().PaddingHorizontal(Px(22)).PaddingTop(Px(18))
                .Element(c => SectionHeading(c, "USLOVI, PRAVILA I POVRAT NOVCA", GreenDark, shortSerial: null));

            column.Item().PaddingHorizontal(Px(22)).PaddingTop(Px(14)).Column(terms =>
            {
                terms.Spacing(Px(10));

                terms.Item().Row(row =>
                {
                    row.Spacing(Px(22));
                    row.RelativeItem().Element(c => Term(c, "VAŽENJE",
                        "Jedan ulaz po ulaznici. QR kod prestaje važiti u trenutku skeniranja. Umnoženi, " +
                        "fotografisani ili proslijeđeni kodovi se odbijaju na ulazu — prvo skeniranje je " +
                        "važeće, a kasnijim donosiocima se ulaz uskraćuje bez povrata novca."));
                    row.RelativeItem().Element(c => Term(c, "PRIJENOS I PREPRODAJA",
                        "Ulaznice su lične i ne smiju se preprodavati iznad nominalne cijene niti koristiti " +
                        "u komercijalne svrhe. Prijenos je važeći samo kroz eKarta aplikaciju, koja izdaje " +
                        "novi serijski broj i poništava ovaj list."));
                });

                terms.Item().Row(row =>
                {
                    row.Spacing(Px(22));
                    row.RelativeItem().Element(c => Term(c, "OTKAZIVANJE I POVRAT NOVCA",
                        "Ako organizator otkaže ili odgodi događaj, puni iznos se vraća na originalno " +
                        "sredstvo plaćanja u roku od 14 dana. Naknada za uslugu se ne vraća. Nema povrata " +
                        "novca za zakašnjeli dolazak, uskraćen ulaz ili neiskorištenu ulaznicu."));
                    row.RelativeItem().Element(c => Term(c, "PRAVILA OBJEKTA I PRIVATNOST",
                        "Ulazak podrazumijeva pristanak na sigurnosnu provjeru i kućni red objekta. " +
                        "Zabranjeni predmeti, pirotehnika i alkohol bit će oduzeti. Objekat može snimati " +
                        "audio i video; lični podaci se obrađuju isključivo radi kontrole pristupa i podrške."));
                });
            });

            column.Item().Extend().AlignBottom().Element(ComposeSupportFooter);
        });
    }

    private static void Term(IContainer container, string title, string body)
    {
        container.Column(column =>
        {
            column.Item().Text(title).Style(Label(10, GreenDark, letterSpacing: 0.12f, strong: true));
            column.Item().PaddingTop(Px(4)).Text(body).Style(Body(10, 1.5f));
        });
    }

    private void ComposeSupportFooter(IContainer container)
    {
        container.Background(GreenDark).PaddingVertical(Px(14)).PaddingHorizontal(Px(22)).Row(row =>
        {
            row.RelativeItem().Column(support =>
            {
                support.Item().Text("PODRŠKA ZA ULAZNICE").Style(Label(9, GreenLight));
                support.Item().PaddingTop(Px(3)).Text(_support.Display).Style(Emphasis(12, White));
            });

            row.AutoItem().AlignRight().Column(serial =>
            {
                serial.Item().AlignRight().Text("SERIJSKI BROJ").Style(Label(9, GreenLight));
                serial.Item().PaddingTop(Px(3)).AlignRight().Text(ShortSerial).Style(MonoStyle(12, White));
            });
        });
    }

    // ---------------------------------------------------------------------------------------
    // Shared pieces
    // ---------------------------------------------------------------------------------------

    /// <summary>The dashed fold guide between panels, plus its caption.</summary>
    private static void FoldMarker(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(BorderStrong).LineDashPattern([Px(4), Px(4)]);
            column.Item().PaddingTop(Px(1)).PaddingRight(Px(22)).AlignRight()
                .Text("PRESAVIJTE OVDJE").Style(Label(8, letterSpacing: 0.18f));
        });
    }

    private static void SectionHeading(IContainer container, string title, string accent, string? shortSerial)
    {
        container.Row(row =>
        {
            row.AutoItem().AlignMiddle().Width(Px(4)).Height(Px(16)).Background(accent);
            row.AutoItem().PaddingLeft(Px(10)).AlignMiddle().Text(title)
                .Style(Title(13).LetterSpacing(0.02f));

            if (shortSerial is not null)
            {
                row.RelativeItem().AlignMiddle().AlignRight().Text(shortSerial)
                    .Style(MonoStyle(10, TextMuted, strong: false));
            }
        });
    }

    /// <summary>Label above value, the sheet's basic unit. Values are clamped to two lines: sector
    /// names and e-mail addresses are free text with generous length limits, and an unclamped one
    /// would push everything below it out of a fixed-height panel.</summary>
    private static void Fact(IContainer container, string label, string value, string? valueColor = null, float size = 15)
    {
        container.Column(column =>
        {
            column.Item().Text(label).Style(Label(size >= 15 ? 9.5f : 9)).ClampLines(1);
            column.Item().PaddingTop(Px(3)).Text(value).ClampLines(2, "…").Style(Value(size, valueColor ?? TextPrimary));
        });
    }

    /// <summary>Bosnian decimal notation uses a comma, and the design shows "25,00 KM". Formatted
    /// against a fixed culture so the container's locale can't change what a ticket says.</summary>
    private static string Money(decimal amount) =>
        amount.ToString("#,##0.00", BosnianNumbers) + " KM";

    private static readonly NumberFormatInfo BosnianNumbers = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
    };

    private string SectorLine() =>
        _model.TicketTypeName is { } type ? $"{_model.SectorName} · {type}" : _model.SectorName;

    /// <summary>The three modes answer "when is this good for" from different fields entirely, so
    /// there is no single date column to print — see .claude/rules/01-domain.md.</summary>
    private string ValidityLine() => _model.TicketingMode switch
    {
        TicketingMode.DailyEntry => _model.ValidDate is { } d
            ? $"{d:dd.MM.yyyy}."
            : "Datum nije određen",

        TicketingMode.RecurringReservation => _model.ValidFrom is { } from && _model.ValidTo is { } to
            ? $"{from:dd.MM.yyyy}. – {to:dd.MM.yyyy}."
            : "Period nije određen",

        _ => _model.ProductDate?.ToString("dd.MM.yyyy. HH:mm") ?? "Datum nije određen",
    };

    private string EntryBadgeText() => _model.TicketingMode switch
    {
        TicketingMode.DailyEntry => "DNEVNI ULAZ",
        TicketingMode.RecurringReservation => "REZERVACIJA",
        _ => "JEDAN ULAZ",
    };

    private string EntryModeText() => _model.TicketingMode switch
    {
        TicketingMode.DailyEntry => "Dnevna ulaznica",
        TicketingMode.RecurringReservation => "Mjesečna rezervacija",
        _ => "Jednokratni ulaz",
    };

    /// <summary>Only SingleOccurrence has a single showing time; the other two are bought for a day
    /// or a period the buyer chose, so there is nothing to print here.</summary>
    private string StartTimeText() =>
        _model.TicketingMode == TicketingMode.SingleOccurrence && _model.ProductDate is { } date
            ? $"{date:HH:mm}"
            : "—";
}
