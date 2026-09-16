namespace eTicketing.Ticketing.Business.Analytics.Forecasting;

/// <summary>One point of a series as ML.NET sees it — a single float column, which is all SSA
/// consumes. The dates live outside the model: SSA reads position as time, which is exactly why
/// ReportSeries.ToDailySeries zero-fills the gaps before anything reaches here.</summary>
internal sealed class SeriesRecord
{
    public float Value { get; set; }
}
