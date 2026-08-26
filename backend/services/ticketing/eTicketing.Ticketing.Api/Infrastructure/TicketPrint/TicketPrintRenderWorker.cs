using eTicketing.Ticketing.Business.TicketPrint;

namespace eTicketing.Ticketing.Api.Infrastructure.TicketPrint;

/// <summary>
/// Drains the print-batch queue, one batch at a time.
///
/// Strictly serial by design. A 5000-ticket sheet is minutes of CPU-bound QuestPDF work inside the
/// same process that serves the purchase critical path, so rendering two at once would turn an
/// organizer's paperwork into a latency problem for buyers. Serial also means the progress an
/// organizer watches actually advances instead of several batches crawling together.
///
/// Nothing here is the source of truth: the queue is only a nudge, and every outstanding batch is
/// recoverable from the database (see <see cref="ITicketPrintRenderer.RecoverUnfinishedAsync"/>),
/// so a restart mid-render resumes rather than stranding anyone.
/// </summary>
public class TicketPrintRenderWorker : BackgroundService
{
    /// <summary>How long a rendered sheet may sit uncollected before it is destroyed. Generous
    /// enough to cover a long weekend, short enough that thousands of live gate codes are not
    /// sitting in the database indefinitely. The tickets themselves stay valid — only the PDF goes,
    /// and a fresh export reprints it.</summary>
    private static readonly TimeSpan FileRetention = TimeSpan.FromDays(7);

    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(6);

    private readonly TicketPrintQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TicketPrintRenderWorker> _logger;

    public TicketPrintRenderWorker(
        TicketPrintQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<TicketPrintRenderWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverAsync(stoppingToken);
        _ = SweepLoopAsync(stoppingToken);

        _logger.LogInformation("Radnik za štampu ulaznica je spreman.");

        await foreach (var batchId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var renderer = scope.ServiceProvider.GetRequiredService<ITicketPrintRenderer>();
                await renderer.RenderAsync(batchId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The renderer already turns an expected failure into a Failed batch with a
                // message. Reaching here means something below it broke — log it and take the next
                // batch, because one bad row must not stop every future export.
                _logger.LogError(ex, "Neočekivana greška pri obradi izvoza {BatchId}.", batchId);
            }
        }
    }

    private async Task RecoverAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var renderer = scope.ServiceProvider.GetRequiredService<ITicketPrintRenderer>();

            foreach (var id in await renderer.RecoverUnfinishedAsync(ct))
            {
                _queue.Enqueue(id);
            }
        }
        catch (Exception ex)
        {
            // A database that is not up yet must not take the whole host down — migrations run in
            // Program.cs before this point, but a transient failure here costs only the resume.
            _logger.LogError(ex, "Nastavak nedovršenih izvoza nakon pokretanja nije uspio.");
        }
    }

    private async Task SweepLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(SweepInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                using var scope = _scopeFactory.CreateScope();
                var renderer = scope.ServiceProvider.GetRequiredService<ITicketPrintRenderer>();
                await renderer.SweepStaleFilesAsync(FileRetention, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Čišćenje nepreuzetih PDF-ova izvoza nije uspjelo.");
        }
    }
}
