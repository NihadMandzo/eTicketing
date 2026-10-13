namespace eTicketing.Shared.Messaging.Tests.TestFixtures;

/// <summary>A clock the test moves by hand. Starts at the real current time, because the rows it is
/// compared against are stamped with the real time by the audit interceptor.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
