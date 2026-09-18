namespace eTicketing.Ticketing.Business.Reports;

/// <summary>How the sales chart is bucketed. Chosen from the range length rather than requested by
/// the client: 90 daily bars are unreadable and 3 monthly ones are uninformative, so the server
/// picks the unit that fits and tells the client which one it picked (the chart title says
/// "po Danu"/"po Sedmici"/"po Mjesecu").</summary>
public enum ReportBucketUnit
{
    Day,
    Week,
    Month
}
