using eTicketing.Ticketing.Business.Sectors;
using StackExchange.Redis;

namespace eTicketing.Ticketing.Api.Infrastructure.Redis;

/// <summary>
/// Atomic capacity hold backed by a Redis Hash per counter (one hash per Sector, or per
/// (Sector, date) for DailyEntry — see BuildCounterKey). Each still-active hold is one hash
/// field: holdId -> "{quantity}:{expiresAtUnixSeconds}". TryHoldAsync's Lua script sums the
/// quantity of all not-yet-expired holds (lazily HDEL-ing any it finds expired along the way),
/// computes remaining = capacity - used, and only adds the new hold if enough remains — the whole
/// read-then-write happens atomically inside Redis, so N parallel requests against a sector with
/// borderline remaining capacity can never together exceed Capacity (see SPRINT_2 US-2.5's
/// acceptance criterion). This avoids needing Redis keyspace notifications (which would need a
/// background subscriber + `notify-keyspace-events` server config) at the cost of holds expiring
/// "lazily" — an abandoned hold's capacity is only reclaimed the next time that counter is
/// touched, not the instant its TTL elapses. Acceptable for this project's scale.
/// </summary>
public class RedisSectorCapacityLock : ISectorCapacityLock
{
    private const string HoldScript = """
        local all = redis.call('HGETALL', KEYS[1])
        local used = 0
        local i = 1
        while i <= #all do
            local field = all[i]
            local value = all[i + 1]
            local sep = string.find(value, ':')
            local qty = tonumber(string.sub(value, 1, sep - 1))
            local exp = tonumber(string.sub(value, sep + 1))
            if exp >= tonumber(ARGV[2]) then
                used = used + qty
            else
                redis.call('HDEL', KEYS[1], field)
            end
            i = i + 2
        end

        local remaining = tonumber(ARGV[1]) - used
        if remaining < tonumber(ARGV[3]) then
            return 0
        end

        redis.call('HSET', KEYS[1], ARGV[5], ARGV[3] .. ':' .. ARGV[4])
        redis.call('EXPIRE', KEYS[1], 86400)
        return 1
        """;

    private readonly IConnectionMultiplexer _redis;

    public RedisSectorCapacityLock(IConnectionMultiplexer redis) => _redis = redis;

    private IDatabase Db => _redis.GetDatabase();

    private static string BuildCounterKey(Guid sectorId, DateOnly? date) =>
        date is null
            ? $"sector:{sectorId}:capacity"
            : $"sector:{sectorId}:date:{date:yyyy-MM-dd}:capacity";

    private static string BuildHoldInfoKey(string holdId) => $"holdinfo:{holdId}";

    public async Task<HoldResult> TryHoldAsync(Guid sectorId, int capacity, int quantity, DateOnly? date, TimeSpan ttl, CancellationToken ct = default)
    {
        var counterKey = BuildCounterKey(sectorId, date);
        var holdId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(ttl);

        var result = (int)await Db.ScriptEvaluateAsync(
            HoldScript,
            [counterKey],
            [capacity, now.ToUnixTimeSeconds(), quantity, expiresAt.ToUnixTimeSeconds(), holdId]);

        if (result == 0)
            return new HoldResult(false, null, null);

        // Records which counter/hash this holdId belongs to, so ConfirmAsync/ReleaseAsync — which
        // only ever see the holdId, not the sectorId/date — can find it again.
        await Db.StringSetAsync(BuildHoldInfoKey(holdId), counterKey, ttl);

        return new HoldResult(true, holdId, expiresAt.UtcDateTime);
    }

    public async Task ConfirmAsync(string holdId, CancellationToken ct = default)
    {
        var counterKey = await Db.StringGetAsync(BuildHoldInfoKey(holdId));
        if (counterKey.IsNullOrEmpty)
            return; // hold already expired/unknown — nothing to confirm

        var current = await Db.HashGetAsync(counterKey.ToString(), holdId);
        if (current.IsNullOrEmpty)
            return;

        var quantity = current.ToString().Split(':')[0];

        // Purchase succeeded: the decrement becomes permanent — extend the hold's tracked expiry
        // far into the future so it keeps counting as "used" forever instead of being lazily
        // reclaimed by a later TryHoldAsync call.
        await Db.HashSetAsync(counterKey.ToString(), holdId, $"{quantity}:{DateTimeOffset.MaxValue.ToUnixTimeSeconds()}");
    }

    public async Task ReleaseAsync(string holdId, CancellationToken ct = default)
    {
        var counterKey = await Db.StringGetAsync(BuildHoldInfoKey(holdId));
        if (counterKey.IsNullOrEmpty)
            return;

        await Db.HashDeleteAsync(counterKey.ToString(), holdId);
        await Db.KeyDeleteAsync(BuildHoldInfoKey(holdId));
    }
}
