using System.Globalization;
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

    /// <summary>The read-only half of <see cref="HoldScript"/>: sums the quantity of every
    /// not-yet-expired hold and reports what is left. Deliberately does NOT lazily HDEL the expired
    /// ones the way the hold script does — a query must not mutate the counter it is reporting on,
    /// and expired holds are excluded from the sum here anyway, so the answer is identical.</summary>
    private const string RemainingScript = """
        local all = redis.call('HGETALL', KEYS[1])
        local used = 0
        local i = 1
        while i <= #all do
            local value = all[i + 1]
            local sep = string.find(value, ':')
            local qty = tonumber(string.sub(value, 1, sep - 1))
            local exp = tonumber(string.sub(value, sep + 1))
            if exp >= tonumber(ARGV[2]) then
                used = used + qty
            end
            i = i + 2
        end

        return tonumber(ARGV[1]) - used
        """;

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisSectorCapacityLock> _logger;

    public RedisSectorCapacityLock(IConnectionMultiplexer redis, ILogger<RedisSectorCapacityLock> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    private IDatabase Db => _redis.GetDatabase();

    private static string BuildCounterKey(Guid sectorId, DateOnly? date) =>
        date is null
            ? $"sector:{sectorId}:capacity"
            : $"sector:{sectorId}:date:{date:yyyy-MM-dd}:capacity";

    private static string BuildHoldInfoKey(string holdId) => $"holdinfo:{holdId}";

    /// <summary>Reverses BuildCounterKey — "sector:{sectorId}:capacity" or
    /// "sector:{sectorId}:date:{yyyy-MM-dd}:capacity" — back into (SectorId, Date). Returns null if
    /// the key doesn't match either shape (should never happen for a key this class itself wrote).</summary>
    private static (Guid SectorId, DateOnly? Date)? TryParseCounterKey(string counterKey)
    {
        var parts = counterKey.Split(':');
        if (parts.Length == 3 && parts[0] == "sector" && parts[2] == "capacity" && Guid.TryParse(parts[1], out var sectorId))
            return (sectorId, null);

        if (parts.Length == 5 && parts[0] == "sector" && parts[2] == "date" && parts[4] == "capacity"
            && Guid.TryParse(parts[1], out var dailySectorId)
            && DateOnly.TryParseExact(parts[3], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return (dailySectorId, date);
        }

        return null;
    }

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

    public async Task<HeldReservation?> PeekAsync(string holdId, CancellationToken ct = default)
    {
        var counterKey = await Db.StringGetAsync(BuildHoldInfoKey(holdId));
        if (counterKey.IsNullOrEmpty)
            return null;

        var current = await Db.HashGetAsync(counterKey.ToString(), holdId);
        if (current.IsNullOrEmpty)
            return null;

        var parsed = TryParseCounterKey(counterKey.ToString()!);
        if (parsed is null)
        {
            _logger.LogError("holdinfo:{HoldId} pointed at counter key {CounterKey}, which does not match either known shape", holdId, counterKey.ToString());
            return null;
        }

        var quantity = int.Parse(current.ToString().Split(':')[0], CultureInfo.InvariantCulture);
        return new HeldReservation(parsed.Value.SectorId, parsed.Value.Date, quantity);
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

        // The hash *value* above is now permanent, but TryHoldAsync's Lua script always sets
        // EXPIRE <counterKey> 86400 regardless — if nothing else touches this counter within 24h
        // of the last hold, Redis would still expire the whole key and silently forget every
        // "permanently confirmed" hold, letting a later TryHoldAsync oversell already-sold
        // capacity. Remove the TTL on the counter key itself so a confirmed hold survives forever.
        await Db.KeyPersistAsync(counterKey.ToString());

        // Once confirmed, this holdId must stop resolving via PeekAsync — otherwise a replayed
        // purchase request (same holdId re-submitted after the first attempt already charged and
        // confirmed) would see the same hold as still "live" and charge again. Deleting the
        // holdinfo pointer makes PeekAsync return null for it from now on, same as an expired hold.
        await Db.KeyDeleteAsync(BuildHoldInfoKey(holdId));
    }

    /// <summary>
    /// Frees capacity a confirmed hold is still occupying. See ISectorCapacityLock for why this
    /// cannot reuse ReleaseAsync: ConfirmAsync deletes the holdinfo pointer that maps a hold id back
    /// to its counter, so the counter has to be rebuilt from the sector and date instead.
    ///
    /// HashDelete is naturally idempotent -- deleting a field that is not there is a no-op -- so a
    /// duplicate cancellation webhook cannot hand the same space back twice.
    /// </summary>
    public async Task ReleaseConfirmedAsync(Guid sectorId, DateOnly? date, string holdId, CancellationToken ct = default)
    {
        var counterKey = BuildCounterKey(sectorId, date);

        var removed = await Db.HashDeleteAsync(counterKey, holdId);

        if (removed)
        {
            _logger.LogInformation(
                "Oslobađa se potvrđeni hold {HoldId} na sektoru {SectorId}.", holdId, sectorId);
        }

        // Also drop any lingering pointer, so the hold id cannot resolve again from either direction.
        await Db.KeyDeleteAsync(BuildHoldInfoKey(holdId));
    }

    public async Task ReleaseAsync(string holdId, CancellationToken ct = default)
    {
        var counterKey = await Db.StringGetAsync(BuildHoldInfoKey(holdId));
        if (counterKey.IsNullOrEmpty)
            return;

        await Db.HashDeleteAsync(counterKey.ToString(), holdId);
        await Db.KeyDeleteAsync(BuildHoldInfoKey(holdId));
    }

    public async Task<int> GetRemainingAsync(Guid sectorId, int capacity, DateOnly? date, CancellationToken ct = default)
    {
        var remaining = (int)await Db.ScriptEvaluateAsync(
            RemainingScript,
            [BuildCounterKey(sectorId, date)],
            [capacity, DateTimeOffset.UtcNow.ToUnixTimeSeconds()]);

        // Clamped because the two inputs can disagree: the counter records what was sold against
        // the capacity in force at the time, and an organizer is free to lower Sector.Capacity
        // afterwards. "Minus four seats left" is not a thing anyone can act on — zero is.
        return Math.Max(0, remaining);
    }
}
