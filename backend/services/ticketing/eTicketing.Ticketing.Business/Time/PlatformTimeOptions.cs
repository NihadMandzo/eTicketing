namespace eTicketing.Ticketing.Business.Time;

/// <summary>The time zone the platform's business day runs on. Configurable per deployment because
/// "today" is a property of where the gate physically stands, not of the code.</summary>
public sealed class PlatformTimeOptions
{
    public const string SectionName = "PlatformTime";

    /// <summary>IANA id. .NET 8+ resolves IANA ids on Windows and Windows ids on Linux, so this one
    /// value works both in local development and in the Linux container.</summary>
    public string TimeZoneId { get; set; } = "Europe/Sarajevo";
}
