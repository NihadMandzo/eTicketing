namespace eTicketing.Contracts.Hosting;

/// <summary>
/// Bound from the <c>LoadShedding</c> configuration section. Defaults are sized against the SQL
/// connection pool (<see cref="Persistence.SqlConnectionPooling.DefaultMaxPoolSize"/>): at most about
/// one in-flight request per pooled connection, plus a short queue to absorb a burst.
/// </summary>
public sealed class LoadSheddingOptions
{
    public const string SectionName = "LoadShedding";

    /// <summary>Requests processed at once by this service instance.</summary>
    public int MaxConcurrentRequests { get; set; } = 200;

    /// <summary>Requests allowed to wait for a slot. Everything beyond this is answered 503
    /// immediately, which is the point: a short honest refusal instead of a minute-long hang.</summary>
    public int QueueLimit { get; set; } = 400;

    /// <summary>Sent as <c>Retry-After</c> on a refusal.</summary>
    public int RetryAfterSeconds { get; set; } = 2;
}
