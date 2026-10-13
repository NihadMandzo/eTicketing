using eTicketing.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eTicketing.Shared.Messaging;

/// <summary>
/// Moves committed <see cref="OutboxMessage"/> rows to the broker, oldest first, and deletes each
/// one the broker confirms.
///
/// <para>Modelled on eTicketing.Ticketing's TicketPrintRenderWorker: a scope per pass, a
/// <see cref="PeriodicTimer"/>, and recovery on start that falls out of the design rather than
/// being a special case — a restart simply finds the rows still sitting there.</para>
///
/// <para><b>Publish, then delete, in that order.</b> The reverse would lose an event whenever the
/// process died in between. This way the same event can be published twice instead, which is the
/// trade the outbox makes: at-least-once rather than at-most-once. See
/// <see cref="OutboxMessage"/> on what that means for consumers.</para>
///
/// <para><b>One pass per database at a time.</b> A pass reads rows without marking them as taken,
/// so two replicas of a service would otherwise both publish every row — a duplicate on every
/// event rather than only after a crash. <see cref="IOutboxDispatchLock"/> makes a pass exclusive;
/// a replica that finds it taken skips the tick.</para>
///
/// <para><b>A refused message never blocks the others.</b> The first version stopped the pass on any
/// failure, so one event the broker could never route (no queue bound to its routing key) held every
/// newer event behind it for good — measured under load: one <c>payment.failed</c> row, retried 316
/// times, with 5,370 <c>ticket.purchased</c> events behind it that were never attempted, so no PDFs
/// and no confirmation emails. Now a refusal of that one message
/// (<see cref="EventRejectedException"/>) counts the attempt and moves on; only a failure that says
/// the broker itself is unavailable still stops the pass, so an outage does not burn every row's
/// attempt counter. A failed row is retried with backoff (<see cref="IsDue"/>) and never ahead of a
/// fresh row, so a pile of permanently refused rows cannot crowd out new events either. Such rows
/// stay in the table, visible through <c>AttemptCount</c> and <c>LastError</c>, and go out as soon
/// as the queue they need exists. Ordering across a retry is not guaranteed; consumers already drop
/// stale redeliveries by their source timestamp.</para>
///
/// <para><b>A backlog drains in one tick.</b> While a pass comes back with a full batch, the next
/// one runs immediately (up to <see cref="MaxPassesPerTick"/>), instead of one batch every
/// <see cref="PollInterval"/> — which capped each service at about ten events a second.</para>
///
/// <para>Rows are hard-deleted, per this repo's no-soft-delete rule. A dispatched event's record is
/// the message in the broker, not a tombstone here.</para>
/// </summary>
public sealed class OutboxDispatcher<TContext> : BackgroundService
    where TContext : DbContext
{
    /// <summary>How long an idle dispatcher waits between passes. Short enough that the email and
    /// PDF a buyer is waiting on are not noticeably later than they were when the publish happened
    /// inline; long enough not to poll the database pointlessly.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>Rows per pass. Bounded so one pass cannot hold a scope open indefinitely, and so a
    /// broker outage that ends produces a steady drain rather than one enormous burst.</summary>
    internal const int BatchSize = 50;

    /// <summary>Back-to-back passes allowed in one tick while batches keep coming back full —
    /// 1,000 rows, after which the lock is released and the next tick carries on.</summary>
    internal const int MaxPassesPerTick = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRawEventPublisher _publisher;
    private readonly IOutboxDispatchLock _dispatchLock;
    private readonly ILogger<OutboxDispatcher<TContext>> _logger;
    private readonly TimeProvider _time;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IRawEventPublisher publisher,
        IOutboxDispatchLock dispatchLock,
        ILogger<OutboxDispatcher<TContext>> logger,
        TimeProvider? time = null)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _dispatchLock = dispatchLock;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A database that is not up yet, or a broker that is not accepting connections.
                // Neither is a reason to stop dispatching for the life of the process — the rows
                // are still there and the next pass tries again.
                _logger.LogError(ex, "Prolaz outbox dispatchera nije uspio.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>One tick: as many passes as a backlog needs, up to <see cref="MaxPassesPerTick"/>,
    /// under one acquisition of the dispatch lock. Internal rather than private so the tests can
    /// drive it directly instead of starting a hosted service and waiting on the timer.</summary>
    internal async Task DispatchPendingAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        if (!await _dispatchLock.TryAcquireAsync(context, ct))
            return;

        try
        {
            for (var pass = 0; pass < MaxPassesPerTick; pass++)
            {
                if (!await PublishPendingAsync(context, ct))
                    break;
            }
        }
        finally
        {
            await _dispatchLock.ReleaseAsync(context);
        }
    }

    /// <summary>Retry schedule for a row that failed: a minute while it is new to failing, then ten,
    /// then hourly. A row the broker keeps refusing costs one attempt an hour, not one every pass.</summary>
    private static readonly (int BelowAttempts, TimeSpan Wait)[] Backoff =
    [
        (5, TimeSpan.FromMinutes(1)),
        (20, TimeSpan.FromMinutes(10)),
        (int.MaxValue, TimeSpan.FromHours(1)),
    ];

    /// <summary>Fresh rows always; failed rows once their backoff has elapsed since the last attempt
    /// (UpdatedAt, stamped by the audit interceptor when the attempt was recorded).</summary>
    private IQueryable<OutboxMessage> IsDue(IQueryable<OutboxMessage> rows)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var firstCutoff = now - Backoff[0].Wait;
        var secondCutoff = now - Backoff[1].Wait;
        var lastCutoff = now - Backoff[2].Wait;
        var firstTier = Backoff[0].BelowAttempts;
        var secondTier = Backoff[1].BelowAttempts;

        return rows.Where(m =>
            m.AttemptCount == 0
            || (m.AttemptCount < firstTier && m.UpdatedAt <= firstCutoff)
            || (m.AttemptCount < secondTier && m.UpdatedAt <= secondCutoff)
            || m.UpdatedAt <= lastCutoff);
    }

    /// <summary>One batch. Returns true when it was full and went out without the broker becoming
    /// unavailable — i.e. there is probably more to send right now.</summary>
    private async Task<bool> PublishPendingAsync(TContext context, CancellationToken ct)
    {
        var pending = await IsDue(context.Set<OutboxMessage>())
            // Fresh rows first, so rows that keep failing can never crowd new events out of a batch.
            .OrderBy(m => m.AttemptCount == 0 ? 0 : 1)
            .ThenBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return false;

        var brokerUnavailable = false;
        var refused = new Dictionary<string, int>();

        foreach (var message in pending)
        {
            try
            {
                await _publisher.PublishRawAsync(message.RoutingKey, message.Payload, message.Id, ct);
                context.Set<OutboxMessage>().Remove(message);
            }
            catch (EventRejectedException ex)
            {
                // This message only. Kept with the attempt recorded, retried on the backoff schedule,
                // and the rest of the batch goes on.
                RecordFailure(message, ex);
                refused[message.RoutingKey] = refused.GetValueOrDefault(message.RoutingKey) + 1;
            }
            catch (Exception ex)
            {
                // Kept, not dropped — that is the entire point. Recording the attempt makes a row
                // the broker keeps refusing visible; without it a permanently poisonous payload
                // would be retried for ever and look exactly like an idle queue.
                RecordFailure(message, ex);

                _logger.LogError(
                    ex,
                    "Objava outbox poruke {MessageId} ({RoutingKey}) nije uspjela, pokušaj {Attempt}.",
                    message.Id, message.RoutingKey, message.AttemptCount);

                // Stop the pass: this is the broker or the connection, not the message, and ploughing
                // on would burn every row's attempt counter on the same outage.
                brokerUnavailable = true;
                break;
            }
        }

        await context.SaveChangesAsync(ct);

        // One line per routing key per pass rather than one per row: a key with no queue bound can
        // refuse hundreds of rows at once.
        foreach (var (routingKey, count) in refused)
        {
            _logger.LogWarning(
                "Broker je odbio {Count} outbox poruka ({RoutingKey}) — vjerovatno nijedan red nije vezan za taj ključ. Pokušavam ponovo kasnije.",
                count, routingKey);
        }

        return !brokerUnavailable && pending.Count == BatchSize;
    }

    private static void RecordFailure(OutboxMessage message, Exception ex)
    {
        message.AttemptCount++;
        message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
    }
}
