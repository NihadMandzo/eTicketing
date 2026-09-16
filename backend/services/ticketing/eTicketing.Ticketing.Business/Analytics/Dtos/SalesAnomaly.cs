namespace eTicketing.Ticketing.Business.Analytics;

/// <summary>One unusual day. <paramref name="ExpectedRevenue"/> is what the series predicted, so
/// the card can say what was surprising rather than only that something was.</summary>
public sealed record SalesAnomaly(
    DateOnly Date,
    string Label,
    AnomalyDirection Direction,
    decimal Revenue,
    decimal ExpectedRevenue,
    decimal DeviationPercent,
    // 0-1. The SSA path reports 1 - p-value; the robust fallback reports a normalized z-score, so
    // the two are comparable enough to rank on and never presented as a probability.
    double Confidence);
