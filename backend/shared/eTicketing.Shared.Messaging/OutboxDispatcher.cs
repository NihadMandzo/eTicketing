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

    /// <summary>Rows per pass. Bounded so one backlog cannot hold a scope open indefinitely, and
    /// so a broker outage that ends produces a steady drain rather than one enormous burst.</summary>
    private const int BatchSize = 50;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRawEventPublisher _publisher;
    private readonly IOutboxDispatchLock _dispatchLock;
    private readonly ILogger<OutboxDispatcher<TContext>> _logger;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IRawEventPublisher publisher,
        IOutboxDispatchLock dispatchLock,
        ILogger<OutboxDispatcher<TContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _dispatchLock = dispatchLock;
        _logger = logger;
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

    /// <summary>One pass. Internal rather than private so the tests can drive it directly instead
    /// of starting a hosted service and waiting on the timer — what is worth pinning is what a pass
    /// does, not that <see cref="PeriodicTimer"/> ticks.</summary>
    internal async Task DispatchPendingAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        if (!await _dispatchLock.TryAcquireAsync(context, ct))
            return;

        try
        {
            await PublishPendingAsync(context, ct);
        }
        finally
        {
            await _dispatchLock.ReleaseAsync(context);
        }
    }

    private async Task PublishPendingAsync(TContext context, CancellationToken ct)
    {
        var pending = await context.Set<OutboxMessage>()
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        foreach (var message in pending)
        {
            try
            {
                await _publisher.PublishRawAsync(message.RoutingKey, message.Payload, message.Id, ct);
                context.Set<OutboxMessage>().Remove(message);
            }
            catch (Exception ex)
            {
                // Kept, not dropped — that is the entire point. Recording the attempt makes a row
                // the broker keeps refusing visible; without it a permanently poisonous payload
                // would be retried for ever and look exactly like an idle queue.
                message.AttemptCount++;
                message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

                _logger.LogError(
                    ex,
                    "Objava outbox poruke {MessageId} ({RoutingKey}) nije uspjela, pokušaj {Attempt}.",
                    message.Id, message.RoutingKey, message.AttemptCount);

                // Stop the pass rather than working through the rest: a broker failure is almost
                // always about the broker, not this message, and ploughing on would burn every
                // row's attempt counter on the same outage. Order is preserved as a side effect.
                break;
            }
        }

        await context.SaveChangesAsync(ct);
    }
}
