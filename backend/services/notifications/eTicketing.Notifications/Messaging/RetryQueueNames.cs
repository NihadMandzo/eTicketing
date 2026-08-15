namespace eTicketing.Notifications.Messaging;

/// <summary>Pure, directly-unit-testable mapping from retry attempt number to the queue a
/// failed-but-retryable message should be republished to. See RabbitMqTopology for how each of
/// these queues is declared (TTL + dead-letter-back-to-Main).</summary>
public static class RetryQueueNames
{
    public const string Main = "notifications.email";
    public const string DeadLetter = "notifications.email.deadletter";

    private const string Tier1_10s = "notifications.email.retry.10s";
    private const string Tier2_1m = "notifications.email.retry.1m";
    private const string Tier3_5m = "notifications.email.retry.5m";
    private const string Tier4_15m = "notifications.email.retry.15m";
    private const string Tier5_30mSteadyState = "notifications.email.retry.30m";

    /// <summary>All retry-tier queue names, in order — used by RabbitMqTopology to declare each
    /// one with its matching TTL.</summary>
    public static readonly IReadOnlyList<(string QueueName, int TtlMilliseconds)> Tiers =
    [
        (Tier1_10s, 10_000),
        (Tier2_1m, 60_000),
        (Tier3_5m, 300_000),
        (Tier4_15m, 900_000),
        (Tier5_30mSteadyState, 1_800_000),
    ];

    /// <summary>attemptNumber 1..4 escalate through the ladder; 5 and every attempt after stay
    /// on the 30-minute tier — retries never stop, they just stop getting further apart. This is
    /// what makes "an email is never lost, it's sent the moment Brevo is available again" true
    /// for an outage of any length.</summary>
    public static string ForAttempt(int attemptNumber) => attemptNumber switch
    {
        1 => Tier1_10s,
        2 => Tier2_1m,
        3 => Tier3_5m,
        4 => Tier4_15m,
        _ => Tier5_30mSteadyState,
    };
}
