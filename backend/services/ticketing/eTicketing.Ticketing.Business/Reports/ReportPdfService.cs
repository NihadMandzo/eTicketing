using System.Security.Claims;
using eTicketing.Contracts.Results;
using eTicketing.Shared.TicketPdf;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Business.Time;
using QuestPDF.Fluent;
using static eTicketing.Ticketing.Business.Reports.ReportFormatting;

namespace eTicketing.Ticketing.Business.Reports;

/// <inheritdoc cref="IReportPdfService"/>
public class ReportPdfService : IReportPdfService
{
    private readonly IReportService _reports;
    private readonly PlatformClock _clock;

    public ReportPdfService(IReportService reports, PlatformClock clock)
    {
        _reports = reports;
        _clock = clock;
    }

    public async Task<Result<ReportPdfResult>> ExportAsync(
        ReportExportQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        // The only rule the underlying reports do not already cover: OrganizationAdmin may read
        // its own sales and product figures on screen but may not carry them out of the app.
        if (user.IsInRole("OrganizationAdmin"))
        {
            return Result<ReportPdfResult>.Failure(Error.Unauthorized(
                "report.export_forbidden", "Nemate dozvolu za izvoz izvještaja."));
        }

        var range = new ReportQuery { From = query.From, To = query.To };

        // Each branch returns the failure of its own report untouched — a role asking to export a
        // tab it cannot see gets the same report.forbidden it would get from the tab itself,
        // rather than a second, differently-worded refusal.
        var model = query.Tab switch
        {
            ReportTab.Sales => await BuildAsync(_reports.GetSalesAsync(range, user, ct), BuildSales),
            ReportTab.Products => await BuildAsync(_reports.GetProductsAsync(range, user, ct), BuildProducts),
            ReportTab.Redemption => await BuildAsync(_reports.GetRedemptionAsync(range, user, ct), BuildRedemption),
            ReportTab.Organizations => await BuildAsync(_reports.GetOrganizationsAsync(range, user, ct), BuildOrganizations),
            _ => Result<ReportPdfModel>.Failure(Error.Validation("report.unknown_tab", "Nepoznat tip izvještaja."))
        };

        if (model.IsFailure)
            return Result<ReportPdfResult>.Failure(model.Error);

        var bytes = new ReportDocument(model.Value!).GeneratePdf();
        return new ReportPdfResult(bytes, FileName(query));
    }

    /// <summary>Awaits one report and maps it into a PDF model, propagating its failure verbatim.
    /// Exists so the four switch arms above stay one line each instead of four copies of the same
    /// IsFailure dance.</summary>
    private static async Task<Result<ReportPdfModel>> BuildAsync<T>(
        Task<Result<T>> pending, Func<T, ReportPdfModel> build)
    {
        var result = await pending;
        return result.IsFailure
            ? Result<ReportPdfModel>.Failure(result.Error)
            : Result<ReportPdfModel>.Success(build(result.Value!));
    }

    private string Footer() =>
        $"Izvještaj generisan {Date(_clock.Today())} · eKarta";

    private static string FileName(ReportExportQuery query)
    {
        var slug = query.Tab switch
        {
            ReportTab.Sales => "prodaja",
            ReportTab.Products => "proizvodi",
            ReportTab.Redemption => "iskoristenost",
            _ => "organizacije"
        };
        return $"izvjestaj-{slug}-{query.From:yyyy-MM-dd}-{query.To:yyyy-MM-dd}.pdf";
    }

    // ── Per-tab model building ───────────────────────────────────────────────────────────────

    private ReportPdfModel BuildSales(SalesReportResponse report)
    {
        // Bars are drawn as a fraction of the tallest one, so an all-zero period draws a flat
        // baseline rather than dividing by zero.
        var peak = report.Buckets.Count == 0 ? 0m : report.Buckets.Max(b => b.Revenue);

        return new ReportPdfModel(
            Title: "Izvještaj o prodaji",
            Scope: report.Scope,
            RangeLabel: Range(report.Period.From, report.Period.To),
            GeneratedAtLabel: Footer(),
            Tiles:
            [
                new ReportPdfTile("Ukupan prihod", Money(report.GrossRevenue),
                    report.RevenueChangePercent is null
                        ? "Nema podataka za prethodni period"
                        : $"{SignedPercent(report.RevenueChangePercent)} u odnosu na prethodni period",
                    ReportPdfEmphasis.Positive),
                new ReportPdfTile("Prodanih karata", Count(report.TicketsSold)),
                new ReportPdfTile("Prosječna cijena karte", Money(report.AverageTicketPrice)),
            ],
            ChartTitle: $"Prihod {BucketTitle(report.Period.BucketUnit)}",
            Chart:
            [
                .. report.Buckets.Select(b => new ReportPdfBar(
                    b.Label,
                    Money(b.Revenue),
                    peak == 0 ? 0f : (float)(b.Revenue / peak)))
            ]);
    }

    private ReportPdfModel BuildProducts(ProductReportResponse report) =>
        new(Title: "Učinak proizvoda",
            Scope: report.Scope,
            RangeLabel: Range(report.Period.From, report.Period.To),
            GeneratedAtLabel: Footer(),
            Tiles:
            [
                new ReportPdfTile("Prodanih karata", Count(report.TotalSold)),
                new ReportPdfTile("Ukupan prihod", Money(report.TotalRevenue), null, ReportPdfEmphasis.Positive),
                new ReportPdfTile("Prosječna cijena", Money(report.AveragePrice)),
                new ReportPdfTile("Prosječna popunjenost", Percent(report.AverageOccupancyPercent)),
            ],
            Table: new ReportPdfTable(
                Title: $"Proizvodi ({Count(report.Rows.Count)})",
                Columns:
                [
                    new ReportPdfColumn("Proizvod", 2.8f),
                    new ReportPdfColumn("Prodano", 1f, RightAligned: true),
                    new ReportPdfColumn("Popunjenost", 1.3f, RightAligned: true),
                    new ReportPdfColumn("Pros. cijena", 1.3f, RightAligned: true),
                    new ReportPdfColumn("Prihod", 1.4f, RightAligned: true),
                ],
                Rows:
                [
                    .. report.Rows.Select(r => (IReadOnlyList<string>)new[]
                    {
                        $"{r.Name}\n{r.Meta}",
                        Count(r.Sold),
                        Percent(r.OccupancyPercent),
                        Money(r.AveragePrice),
                        Money(r.Revenue),
                    })
                ],
                TotalsRow:
                [
                    "Ukupno",
                    Count(report.TotalSold),
                    Percent(report.AverageOccupancyPercent),
                    Money(report.AveragePrice),
                    Money(report.TotalRevenue),
                ]));

    private ReportPdfModel BuildRedemption(RedemptionReportResponse report)
    {
        var peak = report.CheckinsByHour.Count == 0 ? 0 : report.CheckinsByHour.Max(h => h.Count);

        return new ReportPdfModel(
            Title: "Iskorištenost karata",
            Scope: report.Scope,
            RangeLabel: Range(report.Period.From, report.Period.To),
            GeneratedAtLabel: Footer(),
            Tiles:
            [
                new ReportPdfTile("Ukupno skenirano", Count(report.TotalCheckedIn), "Na ulazima"),
                new ReportPdfTile("Stopa nedolaska", Percent(report.NoShowRatePercent),
                    "Prodane, neiskorištene karte", ReportPdfEmphasis.Negative),
                new ReportPdfTile("Vrhunac dolaska", HourWindow(report.PeakHour),
                    report.PeakHourSharePercent is null
                        ? null
                        : $"{Percent(report.PeakHourSharePercent)} svih skeniranja"),
                new ReportPdfTile("Štampane karte", Count(report.PrintedTickets), "Prodane na blagajni"),
            ],
            ChartTitle: "Dolazak po satu",
            Chart:
            [
                .. report.CheckinsByHour.Select(h => new ReportPdfBar(
                    $"{h.Hour:00}h", Count(h.Count), peak == 0 ? 0f : (float)h.Count / peak))
            ],
            Table: new ReportPdfTable(
                Title: "Iskorištenost po događaju",
                Columns:
                [
                    new ReportPdfColumn("Događaj", 2.6f),
                    new ReportPdfColumn("Prodano", 1f, RightAligned: true),
                    new ReportPdfColumn("Iskorišteno", 1f, RightAligned: true),
                    new ReportPdfColumn("Nije došlo", 1f, RightAligned: true),
                    new ReportPdfColumn("Štampane", 1f, RightAligned: true),
                    new ReportPdfColumn("Stopa", 0.9f, RightAligned: true),
                ],
                Rows:
                [
                    .. report.Rows.Select(r => (IReadOnlyList<string>)new[]
                    {
                        r.Name,
                        Count(r.Sold),
                        Count(r.CheckedIn),
                        Count(r.NoShow),
                        Count(r.Printed),
                        Percent(r.RatePercent),
                    })
                ]));
    }

    private ReportPdfModel BuildOrganizations(OrganizationReportResponse report)
    {
        var financial = report.View == OrganizationReportView.Financial;

        // The two column sets of the design, kept apart here rather than rendered as one wide
        // table with blank halves — an Admin's export should not have four empty money columns.
        var columns = financial
            ? new List<ReportPdfColumn>
            {
                new("Organizacija", 2.4f),
                new("Proizvodi", 0.9f, RightAligned: true),
                new("Karte", 1f, RightAligned: true),
                new("Pros. cijena", 1.2f, RightAligned: true),
                new("Rast", 1f, RightAligned: true),
                new("Prihod", 1.4f, RightAligned: true),
            }
            : new List<ReportPdfColumn>
            {
                new("Organizacija", 2.4f),
                new("Proizvodi", 1f, RightAligned: true),
                new("Objavljeno", 1f, RightAligned: true),
                new("Na čekanju", 1f, RightAligned: true),
                new("Bez slike", 1f, RightAligned: true),
                new("Status", 1.2f),
            };

        IReadOnlyList<string> Row(OrganizationReportRow r) => financial
            ? new[]
            {
                RowTitle(r),
                Count(r.Products),
                Count(r.Tickets ?? 0),
                Money(r.AveragePrice ?? 0m),
                SignedPercent(r.GrowthPercent),
                Money(r.Revenue ?? 0m),
            }
            : new[]
            {
                RowTitle(r),
                Count(r.Products),
                Count(r.Published ?? 0),
                Count(r.Pending ?? 0),
                Count(r.WithoutImage ?? 0),
                r.IsPending == true ? "Na čekanju" : "Aktivna",
            };

        var tiles = financial
            ? new List<ReportPdfTile>
            {
                new("Organizacija", Count(report.Rows.Count)),
                new("Ukupno karata", Count(report.Rows.Sum(r => r.Tickets ?? 0))),
                new("Ukupan prihod", Money(report.Rows.Sum(r => r.Revenue ?? 0m)), null, ReportPdfEmphasis.Positive),
            }
            : new List<ReportPdfTile>
            {
                new("Organizacija", Count(report.Rows.Count)),
                new("Objavljenih proizvoda", Count(report.Rows.Sum(r => r.Published ?? 0))),
                new("Na čekanju", Count(report.Rows.Sum(r => r.Pending ?? 0)), null, ReportPdfEmphasis.Negative),
                new("Bez slike", Count(report.Rows.Sum(r => r.WithoutImage ?? 0))),
            };

        return new ReportPdfModel(
            Title: "Učinak organizacija",
            Scope: "Platforma",
            RangeLabel: Range(report.Period.From, report.Period.To),
            GeneratedAtLabel: Footer(),
            Tiles: tiles,
            Table: new ReportPdfTable(
                Title: "Organizacije",
                Columns: columns,
                Rows: [.. report.Rows.Select(Row)]));
    }

    /// <summary>Name over address, the design's two-line first cell. Falls back to the name alone
    /// when the organization has no address on file.</summary>
    private static string RowTitle(OrganizationReportRow row) =>
        string.IsNullOrWhiteSpace(row.Address) ? row.Name : $"{row.Name}\n{row.Address}";

    private static string BucketTitle(ReportBucketUnit unit) => unit switch
    {
        ReportBucketUnit.Day => "po danu",
        ReportBucketUnit.Week => "po sedmici",
        _ => "po mjesecu"
    };
}
