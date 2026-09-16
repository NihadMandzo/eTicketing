namespace eTicketing.Ticketing.Business.Reports;

/// <summary>One bar of the Dolazak po Satu chart. <paramref name="Hour"/> is 0-23 local.</summary>
public sealed record CheckinHourPoint(int Hour, int Count);
