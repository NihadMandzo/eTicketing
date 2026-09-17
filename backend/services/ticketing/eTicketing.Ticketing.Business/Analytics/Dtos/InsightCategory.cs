namespace eTicketing.Ticketing.Business.Analytics;

/// <summary>Which part of the business an insight is about. Purely for the card's icon and for
/// grouping in the PDF — no behavior hangs off it.</summary>
public enum InsightCategory
{
    Forecast,
    Anomaly,
    Sales,
    Audience,
    Redemption,
    Catalog
}
