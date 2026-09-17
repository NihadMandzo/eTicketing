namespace eTicketing.Ticketing.Data.Repositories;

/// <summary>Sales for one UTC hour, split into what stands and what was cancelled.
/// <paramref name="HourUtc"/> is truncated to the hour.</summary>
public record HourlySalesFacts(
    DateTime HourUtc, int Sold, decimal Revenue, int Cancelled, decimal CancelledAmount, int OnlineSold, int PrintedSold);
