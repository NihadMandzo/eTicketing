using eTicketing.Ticketing.Business.Reports;
using static eTicketing.Ticketing.Business.Reports.ReportFormatting;

namespace eTicketing.Ticketing.Business.Analytics.Narrative;

/// <summary>The facts a narrative may be written from — and, by construction, the only ones.</summary>
public sealed record NarrativeContext(
    string Scope,
    ReportPeriod Period,
    SalesReportResponse Sales,
    ForecastBlock Forecast,
    AnomalyBlock Anomalies,
    SegmentBlock Segments,
    IReadOnlyList<BusinessInsight> Insights);

/// <summary>
/// Writes the short Bosnian executive summary above the insight cards.
///
/// <para><b>Returning null is a normal outcome, not an error.</b> No provider configured, the model
/// server down, a timeout, a malformed response — all of them answer null and the tab renders
/// without a summary. Implementations must not throw: the deterministic insights are the feature
/// and they are already computed by the time this is called, so failing the request at this point
/// would throw away good work over an optional flourish.</para>
/// </summary>
public interface INarrativeWriter
{
    Task<string?> WriteAsync(NarrativeContext context, CancellationToken ct = default);
}

/// <summary>The default. Registered whenever <see cref="NarrativeOptions.Provider"/> is "None",
/// which is also what every test and every offline demo runs with.</summary>
public sealed class NullNarrativeWriter : INarrativeWriter
{
    public Task<string?> WriteAsync(NarrativeContext context, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}

/// <summary>
/// Builds the two messages sent to the model.
///
/// <para>Lives in the Business layer, not beside the HTTP client, because <i>what may be disclosed
/// to an external model</i> is a business decision. The facts below are aggregates the caller is
/// already authorized to see on screen; no buyer identity, no e-mail, no ticket and no order
/// reference is included, and the segment names are the platform's own fixed vocabulary rather than
/// anything derived from a person.</para>
///
/// <para>The instructions are defensive on purpose. The model is told to use only the supplied
/// figures and not to invent any — an LLM asked to summarize a table will otherwise happily add a
/// plausible fifth number. What comes back is treated as display text and nothing else: it is never
/// parsed, never used to drive a decision, and truncated before it reaches a client.</para>
/// </summary>
public static class NarrativePromptBuilder
{
    public const string SystemPrompt =
        "Ti si poslovni analitičar platforme za prodaju karata. Pišeš isključivo na bosanskom jeziku. " +
        "Na osnovu isključivo dostavljenih brojeva napiši sažetak od najviše 4 rečenice za organizatora. " +
        "Ne izmišljaj nijedan podatak, ne navodi brojeve kojih nema u ulazu i ne dodaj preporuke koje se " +
        "ne mogu potkrijepiti dostavljenim podacima. Piši sažeto, bez uvoda, bez nabrajanja i bez naslova.";

    public static string BuildUserPrompt(NarrativeContext context)
    {
        var lines = new List<string>
        {
            $"Opseg: {context.Scope}",
            $"Period: {Range(context.Period.From, context.Period.To)} ({context.Period.Days} dana)",
            $"Ukupan prihod: {Money(context.Sales.GrossRevenue)}",
            $"Prodano karata: {Count(context.Sales.TicketsSold)}",
            $"Prosječna cijena karte: {Money(context.Sales.AverageTicketPrice)}",
            $"Otkazano: {Count(context.Sales.CancelledCount)} karata ({Percent(context.Sales.CancellationRatePercent)})",
            $"Kanali prodaje: online {Count(context.Sales.OnlineSold)}, šalter {Count(context.Sales.PrintedSold)}",
        };

        if (context.Sales.RevenueChangePercent is { } change)
            lines.Add($"Promjena prihoda u odnosu na prethodni jednako dug period: {SignedPercent(change)}");

        if (context.Forecast.Source != AnalyticsSource.Insufficient)
        {
            // The rung of the ladder goes into the prompt too, so a summary written from a moving
            // average does not read with the confidence of one written from a fitted model.
            var basis = context.Forecast.Source == AnalyticsSource.Model
                ? "model vremenske serije"
                : "procjena na osnovu prosjeka (malo historijskih podataka)";
            lines.Add(
                $"Prognoza za narednih {context.Forecast.Horizon} dana: {Money(context.Forecast.ProjectedRevenue)}, " +
                $"{Count(context.Forecast.ProjectedSold)} karata, osnova: {basis}" +
                (context.Forecast.ChangePercent is { } forecastChange
                    ? $", promjena {SignedPercent(forecastChange)}"
                    : string.Empty));
        }

        if (context.Anomalies.Items.Count > 0)
        {
            lines.Add("Neuobičajeni dani: " + string.Join("; ", context.Anomalies.Items.Select(a =>
                $"{a.Label} {Money(a.Revenue)} naspram očekivanih {Money(a.ExpectedRevenue)}")));
        }

        if (context.Segments.Items.Count > 0)
        {
            lines.Add($"Segmenti kupaca ({context.Segments.WindowLabel}, ukupno {Count(context.Segments.TotalBuyers)}): " +
                string.Join("; ", context.Segments.Items.Select(s =>
                    $"{s.Name} {Percent(s.SharePercent)} kupaca / {Percent(s.RevenueSharePercent)} prihoda")));
        }

        if (context.Insights.Count > 0)
        {
            lines.Add("Već utvrđeni nalazi: " + string.Join("; ", context.Insights.Select(i => i.Title)));
        }

        return string.Join("\n", lines);
    }
}
