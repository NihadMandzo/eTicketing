using eTicketing.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eTicketing.Shared.Messaging;

/// <summary>
/// Keeps the inbox from growing by one row per message for ever.
///
/// <para><b>What the retention has to outlast</b> is how long a repeat of an already-processed
/// message can still arrive: an outbox row republished after a dispatcher crash, or a message
/// redelivered after a consumer's connection dropped. Both are minutes, hours at worst behind a
/// broker outage. Thirty days covers that with a wide margin. A message replayed by hand from a
/// dead-letter queue is not a concern either way — it only reached the dead-letter queue because
/// processing failed, and a failed attempt records nothing.</para>
///
/// <para>Same shape as <see cref="OutboxDispatcher{TContext}"/>: a scope per pass, a
/// <see cref="PeriodicTimer"/>, and failures logged rather than allowed to stop the loop.</para>
/// </summary>
public sealed class InboxCleanupService<TContext> : BackgroundService
    where TContext : DbContext
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    /// <summary>Rows only become eligible a whole retention period after they were written, so
    /// running often buys nothing. Once an hour keeps each delete small.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InboxCleanupService<TContext>> _logger;

    public InboxCleanupService(IServiceScopeFactory scopeFactory, ILogger<InboxCleanupService<TContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = await DeleteExpiredAsync(DateTime.UtcNow, stoppingToken);
                if (deleted > 0)
                    _logger.LogInformation("Obrisano {Count} isteklih inbox zapisa.", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A database that is not up yet. The rows are still there next hour.
                _logger.LogError(ex, "Čišćenje inbox tabele nije uspjelo.");
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

    /// <summary>One pass. Internal and clock-parameterised so the tests can drive it at a chosen
    /// moment instead of waiting for a timer.</summary>
    internal async Task<int> DeleteExpiredAsync(DateTime utcNow, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        var cutoff = utcNow - Retention;

        return await context.Set<InboxMessage>()
            .Where(m => m.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
