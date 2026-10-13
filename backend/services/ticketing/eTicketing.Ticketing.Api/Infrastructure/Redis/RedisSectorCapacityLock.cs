using System.Globalization;
using eTicketing.Ticketing.Business.Sectors;
using StackExchange.Redis;

namespace eTicketing.Ticketing.Api.Infrastructure.Redis;

/// <summary>
/// Atomic capacity hold backed by three Redis keys per counter (one counter per Sector, or per
/// (Sector, date) for DailyEntry — see BuildCounterKey):
/// <list type="bullet">
/// <item><c>{counter}</c> — hash, holdId -> "{quantity}:{expiresAtUnixSeconds}". Every hold that
/// still occupies capacity: pending ones, and confirmed (sold) ones whose expiry is
/// <see cref="ConfirmedExpiry"/>.</item>
/// <item><c>{counter}:used</c> — integer, the sum of the quantities of every field in that hash.
/// This is the invariant every script below maintains.</item>
/// <item><c>{counter}:exp</c> — sorted set of the <i>pending</i> hold ids, scored by expiry.</item>
/// </list>
///
/// <para><b>Why the counter.</b> The first version summed the whole hash on every hold and on every
/// "seats left" read, and kept every sold ticket in it forever — so each hold cost O(tickets already
/// sold), selling a sector out cost O(n²), and because Redis is single-threaded every other Redis
/// call in the service queued behind it. Measured under the flash-sale load test: 44.9 ms per
/// script call against 0.024 ms for a GET, and a 500-seat sale in which every payment intent failed
/// on a Redis timeout (docs/load-testing-report.md). Now a hold reclaims only the holds that have
/// actually expired since the last call (a range read on the sorted set) and compares against one
/// integer, so its cost no longer grows with sales.</para>
///
/// <para>Still atomic: each mutation is one Lua script, so N parallel requests against a counter
/// with borderline capacity can never together exceed Capacity (SPRINT_2 US-2.5). Expiry is still
/// lazy — an abandoned hold's seats come back the next time that counter is held — but "lazy" now
/// costs what expired, not everything ever sold.</para>
///
/// <para><b>No key TTLs, on purpose.</b> The first version put a 24 h EXPIRE on the counter hash on
/// every hold and removed it on confirm — which the next hold put straight back, so a sector left
/// idle for a day forgot every ticket it had sold and could sell them again. Nothing here expires:
/// sold seats must be remembered for the life of the sector. Expired holds leave the hash as they
/// are reclaimed, and Redis drops a hash or sorted set the moment it is empty.</para>
///
/// <para><b>Redis is not the record of what was sold.</b> The Tickets and Subscriptions tables are.
/// A hold never starts a counter from zero: if the counter does not exist at all, the hold script
/// says so, and the caller rebuilds it from the database (CapacityHoldRecovery + ISoldCapacityCounter
/// -> <see cref="SeedAsync"/>) before trying again. So a Redis restart without persistence, a
/// failover or a flush costs one database count per counter — not a resold sector.</para>
///
/// <para><b>Upgrade.</b> A counter written by the first version has a hash and no <c>:used</c> key.
/// The first hold on it rebuilds <c>:used</c> and <c>:exp</c> from the hash in one O(n) pass — once
/// per counter, ever — and the read-only remaining script falls back to the old full sum until
/// then.</para>
/// </summary>
public class RedisSectorCapacityLock : ISectorCapacityLock
{
    /// <summary>Expiry written into the hash for a confirmed hold. Never reached and never in the
    /// pending sorted set, so it is never reclaimed — it occupies capacity until
    /// <see cref="ReleaseConfirmedAsync"/> gives it back.</summary>
    private static readonly long ConfirmedExpiry = DateTimeOffset.MaxValue.ToUnixTimeSeconds();

    /// <summary>The start of the hold script. Rebuilds :used and :exp from the hash if this counter
    /// predates them (see the class summary's "Upgrade"), then reclaims every pending hold whose
    /// expiry has passed. KEYS: hash, used, exp. ARGV[2]: now, ARGV[6]: ConfirmedExpiry.</summary>
    private const string InitAndReclaim = """
        local now = tonumber(ARGV[2])
        if redis.call('EXISTS', KEYS[2]) == 0 then
            local all = redis.call('HGETALL', KEYS[1])
            local used = 0
            for i = 1, #all, 2 do
                local value = all[i + 1]
                local sep = string.find(value, ':')
                local qty = tonumber(string.sub(value, 1, sep - 1))
                local exp = tonumber(string.sub(value, sep + 1))
                if exp >= now then
                    used = used + qty
                    if exp < tonumber(ARGV[6]) then
                        redis.call('ZADD', KEYS[3], exp, all[i])
                    end
                else
                    redis.call('HDEL', KEYS[1], all[i])
                end
            end
            redis.call('SET', KEYS[2], used)
        end

        local expired = redis.call('ZRANGEBYSCORE', KEYS[3], '-inf', '(' .. now)
        for _, id in ipairs(expired) do
            local value = redis.call('HGET', KEYS[1], id)
            if value then
                redis.call('DECRBY', KEYS[2], tonumber(string.sub(value, 1, string.find(value, ':') - 1)))
                redis.call('HDEL', KEYS[1], id)
            end
        end
        if #expired > 0 then
            redis.call('ZREMRANGEBYSCORE', KEYS[3], '-inf', '(' .. now)
        end

        """;

    /// <summary>Refuses to start a counter from nothing. No :used and no hash means either a
    /// counter never used before or one Redis has lost; both look identical from here, and treating
    /// the second as "0 sold" would resell every seat already sold. Returns -1 so the caller rebuilds
    /// it from the database first (CapacityHoldRecovery, then <see cref="SeedScript"/>).</summary>
    private const string RequireCounter = """
        if redis.call('EXISTS', KEYS[2]) == 0 and redis.call('EXISTS', KEYS[1]) == 0 then
            return -1
        end

        """;

    /// <summary>KEYS: hash, used, exp. ARGV: capacity, now, quantity, expiresAt, holdId,
    /// ConfirmedExpiry. Returns 1 if the hold was taken, 0 if there is not enough left, -1 if the
    /// counter does not exist yet.</summary>
    private const string HoldScript = RequireCounter + InitAndReclaim + """
        local used = tonumber(redis.call('GET', KEYS[2]))
        if tonumber(ARGV[1]) - used < tonumber(ARGV[3]) then
            return 0
        end

        redis.call('HSET', KEYS[1], ARGV[5], ARGV[3] .. ':' .. ARGV[4])
        redis.call('ZADD', KEYS[3], tonumber(ARGV[4]), ARGV[5])
        redis.call('INCRBY', KEYS[2], tonumber(ARGV[3]))
        -- A counter written by the first version may still carry its 24 h TTL; nothing here may
        -- expire while it remembers a sold seat (see the class summary).
        redis.call('PERSIST', KEYS[1])
        return 1
        """;

    /// <summary>Creates a missing counter holding ARGV[1] seats — the database's count of what is
    /// taken. Only if it is still missing: when two holds race to rebuild the same counter, the
    /// second finds it already there and leaves it alone. KEYS: hash, used, exp. Returns 1 if it
    /// seeded the counter.</summary>
    private const string SeedScript = """
        if redis.call('EXISTS', KEYS[2]) == 1 or redis.call('EXISTS', KEYS[1]) == 1 then
            return 0
        end
        redis.call('SET', KEYS[2], tonumber(ARGV[1]))
        return 1
        """;

    /// <summary>Pending -> sold. The quantity stays in :used (the seats stay taken); the hold just
    /// leaves the pending set so it is never reclaimed. One script, so the hold cannot be reclaimed
    /// as expired between reading and rewriting it. KEYS: hash, used, exp. ARGV: holdId,
    /// ConfirmedExpiry. Returns 0 if the hold is no longer in the hash.</summary>
    private const string ConfirmScript = """
        local value = redis.call('HGET', KEYS[1], ARGV[1])
        if not value then
            return 0
        end
        local qty = string.sub(value, 1, string.find(value, ':') - 1)
        redis.call('HSET', KEYS[1], ARGV[1], qty .. ':' .. ARGV[2])
        redis.call('ZREM', KEYS[3], ARGV[1])
        redis.call('PERSIST', KEYS[1])
        return 1
        """;

    /// <summary>Gives one hold's seats back, pending or sold. Idempotent: an unknown hold id is a
    /// no-op, so a duplicate cancellation webhook cannot free the same space twice. :used is only
    /// adjusted once it exists — before that, the next hold rebuilds it from the hash, which no
    /// longer contains this hold. KEYS: hash, used, exp. ARGV: holdId. Returns 1 if a hold was
    /// removed.</summary>
    private const string ReleaseScript = """
        local value = redis.call('HGET', KEYS[1], ARGV[1])
        if not value then
            return 0
        end
        redis.call('HDEL', KEYS[1], ARGV[1])
        redis.call('ZREM', KEYS[3], ARGV[1])
        if redis.call('EXISTS', KEYS[2]) == 1 then
            redis.call('DECRBY', KEYS[2], tonumber(string.sub(value, 1, string.find(value, ':') - 1)))
        end
        return 1
        """;

    /// <summary>The read-only counterpart of the hold script: what is left, without reserving or
    /// reclaiming anything — a query must not mutate the counter it reports on. Pending holds that
    /// expired but were not reclaimed yet still count in :used, so their quantity is added back;
    /// that is only what expired since the last hold, not every sale. KEYS: hash, used, exp.
    /// ARGV: capacity, now.</summary>
    private const string RemainingScript = """
        local now = tonumber(ARGV[2])
        if redis.call('EXISTS', KEYS[2]) == 0 then
            local all = redis.call('HGETALL', KEYS[1])
            local used = 0
            for i = 2, #all, 2 do
                local value = all[i]
                local sep = string.find(value, ':')
                if tonumber(string.sub(value, sep + 1)) >= now then
                    used = used + tonumber(string.sub(value, 1, sep - 1))
                end
            end
            return tonumber(ARGV[1]) - used
        end

        local used = tonumber(redis.call('GET', KEYS[2]))
        local expired = redis.call('ZRANGEBYSCORE', KEYS[3], '-inf', '(' .. now)
        for _, id in ipairs(expired) do
            local value = redis.call('HGET', KEYS[1], id)
            if value then
                used = used - tonumber(string.sub(value, 1, string.find(value, ':') - 1))
            end
        end
        return tonumber(ARGV[1]) - used
        """;

    /// <summary>
    /// Marks a holdinfo value as carrying an owner. Versioned rather than simply changing the
    /// format, because a deploy happens while holds are live: every hold minted by the previous
    /// build stored the bare counter key, and those values keep arriving here for the remaining
    /// five minutes of their TTL. A value without this prefix is therefore read as the old shape
    /// with no owner, rather than as corruption — see TryParseHoldInfo.
    /// </summary>
    private const string HoldInfoVersionPrefix = "v2|";

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

    /// <summary>The three keys every script takes, in KEYS order: hash, used, exp.</summary>
    private static RedisKey[] CounterKeys(string counterKey) =>
        [counterKey, $"{counterKey}:used", $"{counterKey}:exp"];

    private static string BuildHoldInfoKey(string holdId) => $"holdinfo:{holdId}";

    /// <summary>"v2|{ownerId}|{counterKey}", with an empty owner segment for a hold that has none.
    /// The pipe is the separator because a counter key is built only from a Guid and an ISO date,
    /// so it can never contain one — while it always contains the colon.</summary>
    private static string BuildHoldInfoValue(string counterKey, Guid? ownerId) =>
        $"{HoldInfoVersionPrefix}{ownerId?.ToString("N") ?? string.Empty}|{counterKey}";

    /// <summary>Reverses <see cref="BuildHoldInfoValue"/>. A value with no version prefix is a
    /// pre-upgrade hold: its counter key is the whole value and it has no recorded owner, which
    /// callers treat as "anyone may release or spend this" for the rest of its TTL. Returns null
    /// only for a value that is versioned but malformed — that is corruption rather than an old
    /// hold, and resolving it as unowned would hand an attacker exactly the gap the version exists
    /// to close, so it fails closed instead.</summary>
    private (string CounterKey, Guid? OwnerId)? TryParseHoldInfo(string holdId, string value)
    {
        if (!value.StartsWith(HoldInfoVersionPrefix, StringComparison.Ordinal))
            return (value, null);

        var parts = value.Split('|', 3);
        if (parts.Length != 3 || parts[2].Length == 0)
        {
            _logger.LogError(
                "holdinfo:{HoldId} carries the {Prefix} version marker but is malformed", holdId, HoldInfoVersionPrefix);
            return null;
        }

        if (parts[1].Length == 0)
            return (parts[2], null);

        if (!Guid.TryParseExact(parts[1], "N", out var ownerId))
        {
            _logger.LogError("holdinfo:{HoldId} carries an owner segment that is not a Guid", holdId);
            return null;
        }

        return (parts[2], ownerId);
    }

    /// <summary>Reads holdinfo and resolves it to a counter key plus owner, or null if the hold is
    /// unknown, expired or unparseable. The three holdId-only operations all start here.</summary>
    private async Task<(string CounterKey, Guid? OwnerId)?> ReadHoldInfoAsync(string holdId)
    {
        var raw = await Db.StringGetAsync(BuildHoldInfoKey(holdId));
        return raw.IsNullOrEmpty ? null : TryParseHoldInfo(holdId, raw.ToString());
    }

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

    public async Task<HoldResult> TryHoldAsync(
        Guid sectorId, int capacity, int quantity, DateOnly? date, TimeSpan ttl, Guid? ownerId, CancellationToken ct = default)
    {
        var counterKey = BuildCounterKey(sectorId, date);
        var holdId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(ttl);

        var result = (int)await Db.ScriptEvaluateAsync(
            HoldScript,
            CounterKeys(counterKey),
            [capacity, now.ToUnixTimeSeconds(), quantity, expiresAt.ToUnixTimeSeconds(), holdId, ConfirmedExpiry]);

        if (result == -1)
            return new HoldResult(false, null, null, CounterMissing: true);

        if (result == 0)
            return new HoldResult(false, null, null);

        // Records which counter/hash this holdId belongs to, so ConfirmAsync/ReleaseAsync — which
        // only ever see the holdId, not the sectorId/date — can find it again, and who it belongs
        // to, so a stolen hold id cannot be released or spent by a different account.
        await Db.StringSetAsync(BuildHoldInfoKey(holdId), BuildHoldInfoValue(counterKey, ownerId), ttl);

        return new HoldResult(true, holdId, expiresAt.UtcDateTime);
    }

    public async Task<HeldReservation?> PeekAsync(string holdId, CancellationToken ct = default)
    {
        var holdInfo = await ReadHoldInfoAsync(holdId);
        if (holdInfo is null)
            return null;

        var (counterKey, ownerId) = holdInfo.Value;

        var current = await Db.HashGetAsync(counterKey, holdId);
        if (current.IsNullOrEmpty)
            return null;

        var parsed = TryParseCounterKey(counterKey);
        if (parsed is null)
        {
            _logger.LogError(
                "holdinfo:{HoldId} pointed at counter key {CounterKey}, which does not match either known shape", holdId, counterKey);
            return null;
        }

        var quantity = int.Parse(current.ToString().Split(':')[0], CultureInfo.InvariantCulture);
        return new HeldReservation(parsed.Value.SectorId, parsed.Value.Date, quantity, ownerId);
    }

    public async Task ConfirmAsync(string holdId, CancellationToken ct = default)
    {
        var holdInfo = await ReadHoldInfoAsync(holdId);
        if (holdInfo is null)
            return; // hold already expired/unknown — nothing to confirm

        // Purchase succeeded: the seats stay taken for good — see ConfirmScript.
        await Db.ScriptEvaluateAsync(ConfirmScript, CounterKeys(holdInfo.Value.CounterKey), [holdId, ConfirmedExpiry]);

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
    /// The release script is idempotent -- an unknown hold id is a no-op -- so a duplicate
    /// cancellation webhook cannot hand the same space back twice.
    /// </summary>
    public async Task ReleaseConfirmedAsync(Guid sectorId, DateOnly? date, string holdId, CancellationToken ct = default)
    {
        var counterKey = BuildCounterKey(sectorId, date);

        var removed = (int)await Db.ScriptEvaluateAsync(ReleaseScript, CounterKeys(counterKey), [holdId]) == 1;

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
        var holdInfo = await ReadHoldInfoAsync(holdId);
        if (holdInfo is null)
            return;

        await Db.ScriptEvaluateAsync(ReleaseScript, CounterKeys(holdInfo.Value.CounterKey), [holdId]);
        await Db.KeyDeleteAsync(BuildHoldInfoKey(holdId));
    }

    public async Task SeedAsync(Guid sectorId, DateOnly? date, int occupied, CancellationToken ct = default)
    {
        var seeded = (int)await Db.ScriptEvaluateAsync(
            SeedScript, CounterKeys(BuildCounterKey(sectorId, date)), [occupied]) == 1;

        // Rebuilding a counter that held seats is worth a line in the log: outside of a brand-new
        // sector (occupied == 0) it means Redis lost data, and somebody should know that happened.
        if (seeded && occupied > 0)
        {
            _logger.LogWarning(
                "Brojač kapaciteta sektora {SectorId} ({Date}) nije postojao u Redisu; obnovljen iz baze sa {Occupied} zauzetih mjesta.",
                sectorId, date?.ToString("yyyy-MM-dd") ?? "-", occupied);
        }
    }

    public async Task<int> GetRemainingAsync(Guid sectorId, int capacity, DateOnly? date, CancellationToken ct = default)
    {
        var remaining = (int)await Db.ScriptEvaluateAsync(
            RemainingScript,
            CounterKeys(BuildCounterKey(sectorId, date)),
            [capacity, DateTimeOffset.UtcNow.ToUnixTimeSeconds()]);

        // Clamped because the two inputs can disagree: the counter records what was sold against
        // the capacity in force at the time, and an organizer is free to lower Sector.Capacity
        // afterwards. "Minus four seats left" is not a thing anyone can act on — zero is.
        return Math.Max(0, remaining);
    }
}
