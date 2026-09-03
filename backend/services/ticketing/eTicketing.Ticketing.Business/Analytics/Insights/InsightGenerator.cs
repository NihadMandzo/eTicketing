using eTicketing.Ticketing.Business.Reports;
using static eTicketing.Ticketing.Business.Reports.ReportFormatting;
using static eTicketing.Ticketing.Business.Reports.ReportMath;

namespace eTicketing.Ticketing.Business.Analytics.Insights;

/// <summary>
/// Everything the rules read. The three analytics blocks plus the three descriptive reports, passed
/// in rather than re-queried — the tab has already paid for those figures, and recomputing them
/// here is how the insight card and the tile above it end up disagreeing about the same number.
///
/// <paramref name="Redemption"/> is null for OrganizationAdmin, the one role that may see this tab
/// but not gate statistics. The no-show rule simply does not fire rather than the tab refusing.
/// </summary>
public sealed record InsightContext(
    ForecastBlock Forecast,
    AnomalyBlock Anomalies,
    SegmentBlock Segments,
    SalesReportResponse Sales,
    ProductReportResponse Products,
    RedemptionReportResponse? Redemption);

public interface IInsightGenerator
{
    IReadOnlyList<BusinessInsight> Generate(InsightContext context);
}

/// <summary>
/// Turns the numbers into the sentences the tab exists to produce.
///
/// <para><b>Deliberately deterministic.</b> This is the part of the feature that must be
/// defensible: every card here can be traced to one threshold and one figure, it produces the same
/// output for the same input on every run, and each rule has a test pinned to its boundary. The LLM
/// narrative sits on top of these cards and is allowed to be absent — these are not.</para>
///
/// <para><b>A rule never fires from an Insufficient block.</b> Saying "prodaja raste" on the
/// strength of four days of data is worse than saying nothing, and the source ladder exists
/// precisely so this layer can tell the difference.</para>
/// </summary>
public sealed class InsightGenerator : IInsightGenerator
{
    // ── Thresholds ───────────────────────────────────────────────────────────────────────────
    // Every one of these is a business judgement, not a statistical one, so they live together
    // where they can be read and argued with rather than scattered through the rules below.

    /// <summary>Below this, a forecast change is noise dressed as a trend.</summary>
    internal const decimal MaterialChangePercent = 5m;

    internal const decimal CancellationWarnPercent = 10m;
    internal const decimal CancellationCriticalPercent = 20m;

    /// <summary>One product carrying more than half the revenue is a concentration risk: if it is
    /// cancelled or rained off, so is the period.</summary>
    internal const decimal ConcentrationPercent = 50m;

    /// <summary>A channel split further from balance than this is worth naming — either the box
    /// office is doing work the storefront should, or online-only buyers are being turned away.</summary>
    internal const decimal ChannelSkewPercent = 70m;

    internal const decimal LowOccupancyPercent = 40m;
    internal const decimal NoShowWarnPercent = 25m;
    internal const decimal ChurnWarnPercent = 40m;

    /// <summary>A segment earning at least twice its headcount share is the one worth protecting.</summary>
    internal const decimal SegmentLeveragePercent = 2m;

    /// <summary>Six cards is what fits the panel and the PDF page without scrolling. Ranked before
    /// cutting, so what survives is the most severe rather than the first written.</summary>
    internal const int MaxInsights = 6;

    public IReadOnlyList<BusinessInsight> Generate(InsightContext context)
    {
        // A range with no activity at all gets exactly one card. Every rule below divides by
        // something this period does not have, and six cards all saying "0" is not an analysis.
        if (context.Sales.TicketsSold == 0 && context.Sales.CancelledCount == 0)
        {
            return
            [
                new BusinessInsight(
                    InsightSeverity.Neutral,
                    InsightCategory.Sales,
                    "Nema podataka za odabrani period",
                    "U ovom periodu nije zabilježena nijedna prodaja, pa nema osnove za analizu. " +
                    "Odaberite širi period ili provjerite da li su proizvodi objavljeni.",
                    null)
            ];
        }

        var insights = new List<BusinessInsight>();

        AddForecast(insights, context);
        AddAnomalies(insights, context);
        AddCancellations(insights, context);
        AddConcentration(insights, context);
        AddChannelMix(insights, context);
        AddOccupancy(insights, context);
        AddNoShow(insights, context);
        AddSegments(insights, context);

        return
        [
            .. insights
                // Severity descending — the organizer opening this tab needs the problem before the
                // compliment. Critical and Warning are the two ends of the enum, so it is ordered
                // explicitly rather than on the ordinal.
                .OrderByDescending(i => Rank(i.Severity))
                .Take(MaxInsights)
        ];
    }

    private static int Rank(InsightSeverity severity) => severity switch
    {
        InsightSeverity.Critical => 3,
        InsightSeverity.Warning => 2,
        InsightSeverity.Positive => 1,
        _ => 0
    };

    // ── Rules ────────────────────────────────────────────────────────────────────────────────

    private static void AddForecast(List<BusinessInsight> insights, InsightContext context)
    {
        var forecast = context.Forecast;
        if (forecast.Source == AnalyticsSource.Insufficient || forecast.ChangePercent is not { } change)
            return;

        if (Math.Abs(change) < MaterialChangePercent)
        {
            insights.Add(new BusinessInsight(
                InsightSeverity.Neutral,
                InsightCategory.Forecast,
                $"Stabilna prodaja u {HorizonLabel.Next(forecast.Horizon)}",
                $"Projekcija prihoda je {Money(forecast.ProjectedRevenue)} ({forecast.ProjectedSold} karata), " +
                $"približno jednako {HorizonLabel.Previous(forecast.Horizon)}.",
                SignedPercent(change)));
            return;
        }

        var rising = change > 0;
        insights.Add(new BusinessInsight(
            rising ? InsightSeverity.Positive : InsightSeverity.Warning,
            InsightCategory.Forecast,
            rising
                ? $"Prodaja raste u {HorizonLabel.Next(forecast.Horizon)}"
                : $"Prodaja opada u {HorizonLabel.Next(forecast.Horizon)}",
            $"Projekcija prihoda je {Money(forecast.ProjectedRevenue)} ({forecast.ProjectedSold} karata), " +
            $"{SignedPercent(change)} u odnosu na {HorizonLabel.Previous(forecast.Horizon)}." +
            // The rung of the ladder is stated in the card itself, not only in the block header —
            // a card that survives into the PDF must carry its own caveat with it.
            (forecast.Source == AnalyticsSource.Heuristic
                ? " Procjena je zasnovana na prosjeku jer nema dovoljno historije za model."
                : string.Empty),
            SignedPercent(change)));
    }

    private static void AddAnomalies(List<BusinessInsight> insights, InsightContext context)
    {
        var anomalies = context.Anomalies.Items;
        if (context.Anomalies.Source == AnalyticsSource.Insufficient || anomalies.Count == 0)
            return;

        var strongest = anomalies.OrderByDescending(a => Math.Abs(a.DeviationPercent)).First();
        var drops = anomalies.Count(a => a.Direction == AnomalyDirection.Drop);

        insights.Add(new BusinessInsight(
            drops > anomalies.Count / 2 ? InsightSeverity.Warning : InsightSeverity.Neutral,
            InsightCategory.Anomaly,
            anomalies.Count == 1 ? "Zabilježen neuobičajen dan" : $"Zabilježeno {anomalies.Count} neuobičajenih dana",
            $"Najizraženiji je {strongest.Label}: prihod {Money(strongest.Revenue)} naspram očekivanih " +
            $"{Money(strongest.ExpectedRevenue)} " +
            $"({(strongest.Direction == AnomalyDirection.Spike ? "iznad" : "ispod")} očekivanog).",
            Percent(Math.Abs(strongest.DeviationPercent))));
    }

    private static void AddCancellations(List<BusinessInsight> insights, InsightContext context)
    {
        var rate = context.Sales.CancellationRatePercent;
        if (rate <= CancellationWarnPercent)
            return;

        var critical = rate > CancellationCriticalPercent;
        insights.Add(new BusinessInsight(
            critical ? InsightSeverity.Critical : InsightSeverity.Warning,
            InsightCategory.Sales,
            critical ? "Visoka stopa otkazivanja" : "Povišena stopa otkazivanja",
            $"Otkazano je {context.Sales.CancelledCount} karata u vrijednosti od " +
            $"{Money(context.Sales.CancelledAmount)}, što je {Percent(rate)} ukupno prodane vrijednosti.",
            Percent(rate)));
    }

    private static void AddConcentration(List<BusinessInsight> insights, InsightContext context)
    {
        var rows = context.Products.Rows;
        var total = context.Products.TotalRevenue;
        if (rows.Count < 2 || total <= 0m)
            return;

        var top = rows.MaxBy(r => r.Revenue)!;
        var share = Round2(Divide(top.Revenue, total) * 100m);
        if (share <= ConcentrationPercent)
            return;

        insights.Add(new BusinessInsight(
            InsightSeverity.Warning,
            InsightCategory.Catalog,
            "Prihod je koncentrisan na jedan proizvod",
            $"\"{top.Name}\" donosi {Percent(share)} ukupnog prihoda u periodu ({Money(top.Revenue)}). " +
            "Otkazivanje ili slabija prodaja tog proizvoda direktno pogađa cijeli period.",
            Percent(share)));
    }

    private static void AddChannelMix(List<BusinessInsight> insights, InsightContext context)
    {
        var sales = context.Sales;
        var total = sales.OnlineSold + sales.PrintedSold;
        if (total == 0)
            return;

        var onlineShare = Round2(Divide(sales.OnlineSold, total) * 100m);
        if (onlineShare is > (100m - ChannelSkewPercent) and < ChannelSkewPercent)
            return;

        var onlineDominant = onlineShare >= ChannelSkewPercent;
        insights.Add(new BusinessInsight(
            InsightSeverity.Neutral,
            InsightCategory.Sales,
            onlineDominant ? "Prodaja je pretežno online" : "Prodaja je pretežno na šalteru",
            $"{Percent(onlineShare)} karata prodano je online ({sales.OnlineSold}), a " +
            $"{Percent(100m - onlineShare)} štampanjem na šalteru ({sales.PrintedSold}).",
            Percent(onlineShare)));
    }

    private static void AddOccupancy(List<BusinessInsight> insights, InsightContext context)
    {
        if (context.Products.AverageOccupancyPercent is not { } occupancy || occupancy >= LowOccupancyPercent)
            return;

        insights.Add(new BusinessInsight(
            InsightSeverity.Warning,
            InsightCategory.Catalog,
            "Niska popunjenost proizvoda",
            $"Prosječna popunjenost je {Percent(occupancy)}. Razmotrite manje sektore, " +
            "nižu cijenu ili dodatnu promociju za proizvode s najslabijom prodajom.",
            Percent(occupancy)));
    }

    private static void AddNoShow(List<BusinessInsight> insights, InsightContext context)
    {
        // Null for OrganizationAdmin, who may not see gate statistics at all. The rule is skipped
        // rather than the tab refusing — see InsightContext.
        if (context.Redemption is not { } redemption || redemption.NoShowRatePercent <= NoShowWarnPercent)
            return;

        insights.Add(new BusinessInsight(
            InsightSeverity.Warning,
            InsightCategory.Redemption,
            "Visok udio nedolazaka",
            $"{Percent(redemption.NoShowRatePercent)} prodanih karata nije iskorišteno na ulazu" +
            (redemption.PeakHour is null
                ? "."
                : $", uz najveću gužvu u {HourWindow(redemption.PeakHour)}."),
            Percent(redemption.NoShowRatePercent)));
    }

    private static void AddSegments(List<BusinessInsight> insights, InsightContext context)
    {
        var segments = context.Segments;
        if (segments.Source == AnalyticsSource.Insufficient || segments.Items.Count == 0)
            return;

        // A segment earning far more than its headcount is the one whose churn would hurt most —
        // and the one worth a loyalty offer. Guarded on a non-zero head share so a rounding
        // artefact cannot produce an infinite ratio.
        var leverage = segments.Items
            .Where(s => s.SharePercent > 0m && s.RevenueSharePercent >= s.SharePercent * SegmentLeveragePercent)
            .MaxBy(s => s.RevenueSharePercent);

        if (leverage is not null)
        {
            insights.Add(new BusinessInsight(
                InsightSeverity.Positive,
                InsightCategory.Audience,
                $"Segment \"{leverage.Name}\" nosi prihod",
                $"Čini {Percent(leverage.SharePercent)} kupaca, a donosi {Percent(leverage.RevenueSharePercent)} prihoda " +
                $"({Money(leverage.AverageSpend)} po kupcu). Zadržavanje ovog segmenta je najisplativija mjera.",
                Percent(leverage.RevenueSharePercent)));
        }

        var dormant = segments.Items.FirstOrDefault(s => s.Name == "Neaktivni kupci");
        if (dormant is not null && dormant.SharePercent > ChurnWarnPercent)
        {
            insights.Add(new BusinessInsight(
                InsightSeverity.Warning,
                InsightCategory.Audience,
                "Velik udio neaktivnih kupaca",
                $"{Percent(dormant.SharePercent)} kupaca ({dormant.Buyers}) nije kupilo ništa duže vrijeme. " +
                "Ponovno aktiviranje postojećih kupaca je jeftinije od privlačenja novih.",
                Percent(dormant.SharePercent)));
        }
    }
}
