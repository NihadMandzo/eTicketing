using eTicketing.Notifications.Messaging;
using StackExchange.Redis;

namespace eTicketing.Notifications.Infrastructure.Redis;

/// <summary>
/// <see cref="IProcessedMessageStore"/> as one Redis key per processed delivery, expiring on its own.
///
/// <para>Redis rather than a table because this service has no database, and the platform already
/// runs Redis for eTicketing.Ticketing's capacity holds. The retention only has to outlast how long a
/// repeat can realistically still arrive — an outbox row stuck behind a broker outage, or a message
/// sitting out the retry ladder — and a week covers both with a wide margin, while keeping the key
/// count bounded without any cleanup job.</para>
/// </summary>
public sealed class RedisProcessedMessageStore : IProcessedMessageStore
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    private const string KeyPrefix = "notifications:processed:";

    private readonly IConnectionMultiplexer _redis;

    public RedisProcessedMessageStore(IConnectionMultiplexer redis) => _redis = redis;

    private IDatabase Db => _redis.GetDatabase();

    public static string BuildKey(string key) => KeyPrefix + key;

    public Task<bool> IsProcessedAsync(string key, CancellationToken ct = default) =>
        Db.KeyExistsAsync(BuildKey(key));

    public Task MarkProcessedAsync(string key, CancellationToken ct = default) =>
        Db.StringSetAsync(BuildKey(key), "1", Retention, When.Always);
}
