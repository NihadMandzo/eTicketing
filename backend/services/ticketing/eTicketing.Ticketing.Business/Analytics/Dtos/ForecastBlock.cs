namespace eTicketing.Ticketing.Business.Analytics;

/// <summary>
/// The forecast block. <paramref name="ChangePercent"/> compares the projected total against the
/// equally-long stretch of actuals immediately before it, so "+18,2%" always means "than the last
/// <c>Horizon</c> days" — null when that stretch sold nothing, same rule as the Prodaja tab, and
/// null too when the selected range is shorter than the horizon, since then no equally-long stretch
/// exists and comparing a projected year against a month of actuals would read as +1.000%.
/// </summary>
public sealed record ForecastBlock(
    AnalyticsSource Source,
    int Horizon,
    decimal ProjectedRevenue,
    int ProjectedSold,
    decimal? ChangePercent,
    // The tail of actuals the projection continues, so the client can draw one unbroken series
    // without re-deriving which days the Prodaja tab happened to bucket together.
    IReadOnlyList<ForecastPoint> Actual,
    IReadOnlyList<ForecastPoint> Points);
