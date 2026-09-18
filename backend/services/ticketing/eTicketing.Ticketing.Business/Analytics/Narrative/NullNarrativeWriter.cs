namespace eTicketing.Ticketing.Business.Analytics.Narrative;

/// <summary>The default. Registered whenever <see cref="NarrativeOptions.Provider"/> is "None",
/// which is also what every test and every offline demo runs with.</summary>
public sealed class NullNarrativeWriter : INarrativeWriter
{
    public Task<string?> WriteAsync(NarrativeContext context, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}
