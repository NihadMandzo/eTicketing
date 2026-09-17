namespace eTicketing.Ticketing.Business.Analytics.Narrative;

/// <summary>Bound from the "Insights" section of appsettings.json.</summary>
public sealed class InsightsOptions
{
    public const string SectionName = "Insights";

    /// <summary>How long a computed AI Uvidi response is reused for.
    ///
    /// Short, not zero. Fitting SSA and K-Means is milliseconds, but the tab also costs three
    /// report queries and up to two cross-service lookups, and the range the user is looking at
    /// does not change between the moment they switch tabs and the moment they hit the horizon
    /// selector. Ten minutes is short enough that a sale made now shows up while the organizer is
    /// still at their desk.</summary>
    public int CacheMinutes { get; set; } = 10;

    public NarrativeOptions Narrative { get; set; } = new();
}
