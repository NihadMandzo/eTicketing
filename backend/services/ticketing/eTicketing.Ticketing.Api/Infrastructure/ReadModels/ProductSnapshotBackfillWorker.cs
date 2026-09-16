using eTicketing.Ticketing.Business.ReadModels;

namespace eTicketing.Ticketing.Api.Infrastructure.ReadModels;

/// <summary>
/// Fills in ProductSnapshot rows this service never received an event for.
///
/// <para>Two jobs in one loop. The first pass is the migration: every product that already had
/// sectors when the read model was introduced has no row, and until it gets one its sectors are
/// correctly — but unhelpfully — treated as unavailable. The passes after that are self-healing,
/// for the events lost while this consumer was down longer than the broker kept them.</para>
///
/// <para>Startup does not block on it and a failure does not stop the service: a backfill that
/// cannot reach eTicketing.Catalog is a reason to retry in six hours, not a reason to refuse to
/// serve the tickets already sold. Every pass is a no-op once there is nothing missing, which is
/// the steady state.</para>
///
/// <para>The equivalent on the organization side lives in eTicketing.Identity rather than here
/// (OrganizationSnapshotRepublishWorker) — Ticketing has no whitelisted synchronous call to
/// Identity to pull from, so that half is pushed instead of pulled.</para>
/// </summary>
public class ProductSnapshotBackfillWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    /// <summary>Long enough for the RabbitMQ consumer to have drained whatever it can reach, so the
    /// first pass usually finds only the genuinely missing rows rather than racing live events into
    /// the same table.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductSnapshotBackfillWorker> _logger;

    public ProductSnapshotBackfillWorker(
        IServiceScopeFactory scopeFactory, ILogger<ProductSnapshotBackfillWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);

        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var projector = scope.ServiceProvider.GetRequiredService<IProductSnapshotProjector>();

            var written = await projector.BackfillAsync(ct);
            if (written > 0)
            {
                _logger.LogInformation(
                    "Dopunjeno {Count} snapshot zapisa o proizvodima iz kataloga.", written);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dopuna snapshot zapisa o proizvodima nije uspjela — pokušavam ponovo u sljedećem ciklusu.");
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
