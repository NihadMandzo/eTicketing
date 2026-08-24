namespace eTicketing.PdfGeneration.Messaging;

/// <summary>Pure, directly-unit-testable mapping from retry attempt number to the queue a
/// failed-but-retryable message should be republished to — the same ladder
/// eTicketing.Notifications uses, with this service's own queue names so the two never share a
/// backlog. See RabbitMqTopology for how each queue is declared (TTL + dead-letter-back-to-Main).
///
/// The ladder is warranted here for the same reason it is in Notifications: this path depends on
/// two things outside the process (eTicketing.Catalog and Azure Blob Storage), and a buyer whose
/// PDF is abandoned never receives their ticket at all — the confirmation email is chained behind
/// this event.</summary>
public static class RetryQueueNames
{
    public const string Main = "pdfgeneration.tickets";
    public const string DeadLetter = "pdfgeneration.tickets.deadletter";

    private const string Tier1_10s = "pdfgeneration.tickets.retry.10s";
    private const string Tier2_1m = "pdfgeneration.tickets.retry.1m";
    private const string Tier3_5m = "pdfgeneration.tickets.retry.5m";
    private const string Tier4_15m = "pdfgeneration.tickets.retry.15m";
    private const string Tier5_30mSteadyState = "pdfgeneration.tickets.retry.30m";

    public static readonly IReadOnlyList<(string QueueName, int TtlMilliseconds)> Tiers =
    [
        (Tier1_10s, 10_000),
        (Tier2_1m, 60_000),
        (Tier3_5m, 300_000),
        (Tier4_15m, 900_000),
        (Tier5_30mSteadyState, 1_800_000),
    ];

    /// <summary>attemptNumber 1..4 escalate through the ladder; 5 and every attempt after stay on
    /// the 30-minute tier — retries never stop, they just stop getting further apart.</summary>
    public static string ForAttempt(int attemptNumber) => attemptNumber switch
    {
        1 => Tier1_10s,
        2 => Tier2_1m,
        3 => Tier3_5m,
        4 => Tier4_15m,
        _ => Tier5_30mSteadyState,
    };
}
