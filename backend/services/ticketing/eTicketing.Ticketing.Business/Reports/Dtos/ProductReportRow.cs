namespace eTicketing.Ticketing.Business.Reports;

/// <summary>
/// One row of the Učinak Proizvoda table. <paramref name="Meta"/> is the small grey second line:
/// the owning organization for platform staff, "N sektora · M karata" for an organizer looking at
/// their own products.
/// </summary>
public sealed record ProductReportRow(
    Guid ProductId,
    string Name,
    string Meta,
    int Sold,
    // Null for DailyEntry products, whose capacity is a per-day allowance rather than a total —
    // see ISectorRepository.GetPublishedCapacityByProductAsync. The UI renders "—".
    decimal? OccupancyPercent,
    decimal AveragePrice,
    int Cancelled,
    decimal Revenue);
