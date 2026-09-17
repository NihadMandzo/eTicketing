namespace eTicketing.Ticketing.Business.Reports;

/// <summary>
/// Sales for one local calendar day. The Data layer cannot produce this — it has no time zone
/// (see PlatformClock.ToLocal) — so it hands back UTC hours and ToLocalDays folds them here.
/// </summary>
internal readonly record struct LocalDaySales(
    int Sold, decimal Revenue, int Cancelled, decimal CancelledAmount, int OnlineSold, int PrintedSold);
