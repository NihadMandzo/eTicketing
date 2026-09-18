using eTicketing.Identity.Business.Organizations;

namespace eTicketing.Identity.Api.Infrastructure;

/// <summary>
/// Republishes every organization snapshot on startup and every six hours after that.
///
/// <para>eTicketing.Ticketing keeps a local copy of organization names, addresses and contact
/// details so it no longer has to call this service synchronously to label a report row or address a
/// cancellation email. That copy has to get filled somehow, and — unlike the product read model,
/// which can pull its own gaps over the whitelisted Ticketing→Catalog call — there is no
/// Ticketing→Identity call left to pull with. Removing it was the point. So the catch-up is pushed
/// from this side, and the same sweep doubles as the repair for anything lost while that consumer
/// was down longer than the broker held the message.</para>
///
/// <para>Cheap by construction: one projection query and one outbox row per organization, on a
/// platform that has tens of them, not millions. If that ever stops being true the fix is a
/// watermark on the sweep, not a longer interval — a projection that heals once a day is a
/// projection that can be wrong for a day.</para>
///
/// <para>A failure is logged and retried next cycle rather than stopping the service: sign-in must
/// not depend on a broker that is having a bad afternoon.</para>
/// </summary>
public class OrganizationSnapshotRepublishWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    /// <summary>Long enough for the outbox dispatcher and the database migration on startup to be
    /// past, so the first sweep publishes into a working pipeline instead of piling up rows.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrganizationSnapshotRepublishWorker> _logger;

    public OrganizationSnapshotRepublishWorker(
        IServiceScopeFactory scopeFactory, ILogger<OrganizationSnapshotRepublishWorker> logger)
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
            var publisher = scope.ServiceProvider.GetRequiredService<IOrganizationSnapshotPublisher>();

            var published = await publisher.RepublishAllAsync(ct);
            _logger.LogInformation(
                "Objavljeno {Count} snapshot zapisa o organizacijama.", published);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Objava snapshot zapisa o organizacijama nije uspjela — pokušavam ponovo u sljedećem ciklusu.");
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
