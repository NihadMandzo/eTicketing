using eTicketing.Ticketing.Business.Tickets;
using StackExchange.Redis;

namespace eTicketing.Ticketing.Api.Infrastructure.Redis;

/// <summary>
/// The classic Redis mutex: <c>SET key token NX EX 10</c> to acquire, compare-then-delete via Lua
/// to release. Two properties matter here and both come from that shape:
///
///  - <b>NX</b> makes acquisition atomic, so of N scanners hitting the same ticket at the same
///    instant exactly one proceeds and the rest get a 409 instead of all reading Confirmed and all
///    writing Used.
///  - <b>The token check on release</b> stops a caller whose request stalled past the TTL from
///    deleting a lock a *later* scan legitimately acquired in the meantime — a plain DEL would
///    reopen exactly the race the lock exists to close.
///
/// The 10-second TTL is the backstop for a process that dies mid-validation: worst case the gate
/// is blocked on that one ticket for 10s, never forever.
/// </summary>
public class RedisTicketValidationLock : ITicketValidationLock
{
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(10);

    private const string ReleaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    private readonly IConnectionMultiplexer _redis;

    public RedisTicketValidationLock(IConnectionMultiplexer redis) => _redis = redis;

    private IDatabase Db => _redis.GetDatabase();

    private static string BuildKey(Guid ticketId) => $"ticket:{ticketId}:validate";

    public async Task<string?> TryAcquireAsync(Guid ticketId, CancellationToken ct = default)
    {
        var token = Guid.NewGuid().ToString("N");
        var acquired = await Db.StringSetAsync(BuildKey(ticketId), token, LockTtl, When.NotExists);
        return acquired ? token : null;
    }

    public Task ReleaseAsync(Guid ticketId, string token, CancellationToken ct = default) =>
        Db.ScriptEvaluateAsync(ReleaseScript, [BuildKey(ticketId)], [token]);
}
