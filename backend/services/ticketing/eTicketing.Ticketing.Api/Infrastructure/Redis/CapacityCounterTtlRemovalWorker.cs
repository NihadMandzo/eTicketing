using StackExchange.Redis;

namespace eTicketing.Ticketing.Api.Infrastructure.Redis;

/// <summary>
/// One pass at startup that removes any TTL left on a capacity counter by the first version of
/// <see cref="RedisSectorCapacityLock"/>.
///
/// <para>That version put a 24 h <c>EXPIRE</c> on the counter hash on every hold, which undid the
/// <c>PERSIST</c> its confirm relied on — so a counter still holding sold seats could expire, and the
/// sector could then sell them again. The rewritten lock never sets a TTL and clears one on the next
/// hold, but a counter nobody holds on again — a sold-out event, typically — would never get that
/// next hold, and would still expire. This closes the gap for every counter at once.</para>
///
/// <para>Uses <c>SCAN</c> (via <see cref="IServer.KeysAsync"/>), which walks the keyspace in small
/// steps without blocking Redis. <c>PERSIST</c> on a key with no TTL is a no-op, so running it on
/// every startup is harmless and needs no "already done" flag. A failure is logged and swallowed: it
/// must never stop Ticketing from starting.</para>
/// </summary>
public sealed class CapacityCounterTtlRemovalWorker : BackgroundService
{
    private const string CounterPattern = "sector:*:capacity";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CapacityCounterTtlRemovalWorker> _logger;

    public CapacityCounterTtlRemovalWorker(IConnectionMultiplexer redis, ILogger<CapacityCounterTtlRemovalWorker> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var db = _redis.GetDatabase();
            var persisted = 0;

            foreach (var endpoint in _redis.GetEndPoints())
            {
                var server = _redis.GetServer(endpoint);
                if (server.IsReplica)
                    continue;

                await foreach (var key in server.KeysAsync(database: db.Database, pattern: CounterPattern).WithCancellation(stoppingToken))
                {
                    if (await db.KeyPersistAsync(key))
                        persisted++;
                }
            }

            _logger.LogInformation(
                "Uklonjen istek (TTL) sa {Count} brojača kapaciteta sektora.", persisted);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uklanjanje isteka (TTL) sa brojača kapaciteta sektora nije uspjelo.");
        }
    }
}
