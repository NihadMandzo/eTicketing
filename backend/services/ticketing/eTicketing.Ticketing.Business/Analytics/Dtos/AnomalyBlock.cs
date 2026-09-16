namespace eTicketing.Ticketing.Business.Analytics;

public sealed record AnomalyBlock(AnalyticsSource Source, IReadOnlyList<SalesAnomaly> Items);
