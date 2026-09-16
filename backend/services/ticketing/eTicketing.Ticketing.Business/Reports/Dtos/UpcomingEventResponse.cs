namespace eTicketing.Ticketing.Business.Reports;

/// <summary>One row of the Dashboard's "Nadolazeći događaji" card — a published SingleOccurrence
/// product with a future date. <paramref name="Meta"/> is the small second line: "{organization} ·
/// {city}" for platform staff (who see every organization), "{N sektora} · {city}" for an organizer
/// (who already knows it's their own). <paramref name="Sold"/>/<paramref name="Capacity"/> reflect
/// the event's current state, not a reporting-period figure — see
/// ReportService.GetUpcomingEventsAsync.</summary>
public sealed record UpcomingEventResponse(
    Guid ProductId, string Name, string Meta, DateTime Date, int Sold, int Capacity);
