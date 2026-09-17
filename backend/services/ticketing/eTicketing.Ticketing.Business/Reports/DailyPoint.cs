namespace eTicketing.Ticketing.Business.Reports;

/// <summary>One day of the gap-free series the analytics blocks are computed over. Public, unlike
/// its neighbours here, because it is the input type of ISalesForecaster/IAnomalyDetector, which
/// the Api project has to name to register them.</summary>
public readonly record struct DailyPoint(DateOnly Date, decimal Revenue, int Sold);
