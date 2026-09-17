using Microsoft.EntityFrameworkCore;

namespace eTicketing.Shared.Messaging;

/// <summary>
/// Makes one <see cref="OutboxDispatcher{TContext}"/> pass exclusive per database.
///
/// <para>A pass reads the oldest rows and publishes them without marking them as taken, so two
/// replicas of the same service polling the same database would both read and both publish every
/// row — a duplicate on every event, not only after a crash. Serializing whole passes is enough to
/// close that: a pass is already sequential inside, so a second replica had nothing to add to its
/// throughput beyond the duplicates.</para>
/// </summary>
public interface IOutboxDispatchLock
{
    /// <summary>Tries once, without waiting. False means another dispatcher holds the lock and this
    /// pass should be skipped — its rows are being drained, and the next tick tries again.</summary>
    Task<bool> TryAcquireAsync(DbContext context, CancellationToken ct);

    /// <summary>Releases a lock <see cref="TryAcquireAsync"/> granted. Called for every granted
    /// acquisition, including passes that failed partway.</summary>
    Task ReleaseAsync(DbContext context);
}
