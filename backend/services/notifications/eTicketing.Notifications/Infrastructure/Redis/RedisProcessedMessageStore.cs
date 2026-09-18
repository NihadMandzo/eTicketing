using eTicketing.Notifications.Messaging;
using StackExchange.Redis;

namespace eTicketing.Notifications.Infrastructure.Redis;

/// <summary>
/// <see cref="IProcessedMessageStore"/> as one Redis key per delivery, expiring on its own.
///
/// <para>Redis rather than a table because this service has no database, and the platform already
/// runs Redis for eTicketing.Ticketing's capacity holds. The key holds
/// <see cref="ProcessingValue"/> while a consumer is sending and <see cref="ProcessedValue"/>
/// afterwards, so one key answers both "is someone on it" and "has it been sent".</para>
///
/// <para><b>The claim is a Lua script, not a check followed by a write.</b> Two replicas handed
/// copies of the same message would otherwise both read "not processed" and both send. The script
/// sets the key only when it is missing, and returns whatever is already there when it is not — one
/// round trip, and the decision is made inside Redis where it cannot be interleaved.</para>
///
/// <para><b>Retention is 30 days</b>, matching the inbox's, and for the same reason: it has to
/// outlast how long a repeat can still arrive — an outbox row republished after a dispatcher crash,
/// or a message that sat out the retry ladder. No finite retention can survive an arbitrarily long
/// outage (a service down for longer than this, still holding an undelivered outbox row, could send
/// a second copy on its return); 30 days puts that far outside any plausible operating window while
/// keeping the key count bounded with no cleanup job.</para>
/// </summary>
public sealed class RedisProcessedMessageStore : IProcessedMessageStore
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public const string ProcessingValue = "processing";
    public const string ProcessedValue = "done";

    private const string KeyPrefix = "notifications:processed:";

    /// <summary>SET-if-missing and read-what-is-there, as one atomic step. Returns the value now
    /// held by the key: this caller's own when it won, somebody else's when it did not.</summary>
    private const string ClaimScript = """
        local current = redis.call('GET', KEYS[1])
        if current then
            return current
        end
        redis.call('SET', KEYS[1], ARGV[1], 'EX', ARGV[2])
        return ARGV[1]
        """;

    /// <summary>Only ever clears this caller's claim. A key that has since been marked processed —
    /// or re-claimed by a later attempt after this one's lease expired — is left alone.</summary>
    private const string ReleaseClaimScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    private readonly IConnectionMultiplexer _redis;

    public RedisProcessedMessageStore(IConnectionMultiplexer redis) => _redis = redis;

    private IDatabase Db => _redis.GetDatabase();

    public static string BuildKey(string key) => KeyPrefix + key;

    public async Task<DeliveryClaim> TryClaimAsync(string key, TimeSpan lease, CancellationToken ct = default)
    {
        var held = await Db.ScriptEvaluateAsync(
            ClaimScript,
            [BuildKey(key)],
            [ProcessingValue, (int)lease.TotalSeconds]);

        return held.ToString() switch
        {
            ProcessingValue => DeliveryClaim.Claimed,
            // Anything else is a finished send: "done", or the bare "1" written by the build that
            // marked processed messages before claims existed.
            _ => DeliveryClaim.AlreadyProcessed,
        };
    }

    public Task MarkProcessedAsync(string key, CancellationToken ct = default) =>
        Db.StringSetAsync(BuildKey(key), ProcessedValue, Retention, When.Always);

    public Task ReleaseClaimAsync(string key, CancellationToken ct = default) =>
        Db.ScriptEvaluateAsync(ReleaseClaimScript, [BuildKey(key)], [ProcessingValue]);
}
