namespace eTicketing.Ticketing.Business.Reports;

public sealed record RedemptionReportRow(
    Guid ProductId,
    string Name,
    int Sold,
    int CheckedIn,
    int NoShow,
    int Printed,
    decimal RatePercent);
