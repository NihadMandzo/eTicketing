using eTicketing.Catalog.Business.Recommendations;
using Microsoft.Extensions.Options;

namespace eTicketing.Catalog.Api.Infrastructure;

/// <summary>
/// Keeps the recommendation model current. Two jobs, in order:
///
///  1. At startup, bring the last trained model back from blob storage. This is the whole point of
///     persisting it — a restarted container resumes recommending immediately instead of serving
///     the popularity fallback until the next nightly run. Only when there is nothing to load does
///     it train from scratch, which is also what makes seeded interaction data produce real
///     recommendations on the first `docker compose up`.
///
///  2. Then retrain once a day at RecommendationOptions.TrainAtHour.
///
/// Nightly rather than continuous because ML.NET's matrix factorization has no incremental-update
/// API — there is no "learn from this one purchase" call, the whole matrix is refactorized. New
/// interactions still take effect immediately in the parts that don't need the model (the
/// already-bought exclusion, the popularity ranking, the content-based fallback); what waits for
/// the nightly run is only the learned latent structure. POST /recommendations/retrain forces it in
/// the meantime.
/// </summary>
public sealed class RecommendationTrainingHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecommendationOptions _options;
    private readonly ILogger<RecommendationTrainingHostedService> _logger;
    private readonly TimeProvider _timeProvider;

    public RecommendationTrainingHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<RecommendationOptions> options,
        ILogger<RecommendationTrainingHostedService> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunSafely(
            (service, ct) => service.EnsureModelLoadedAsync(ct),
            "Učitavanje modela preporuka pri pokretanju nije uspjelo.",
            stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun();
            _logger.LogInformation(
                "Sljedeće treniranje modela preporuka za {Hours}h {Minutes}min.", (int)delay.TotalHours, delay.Minutes);

            try
            {
                await Task.Delay(delay, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunSafely(
                async (service, ct) =>
                {
                    var result = await service.RetrainAsync(ct);
                    if (result.IsFailure)
                        _logger.LogInformation("Noćno treniranje preskočeno: {Reason}", result.Error.Message);
                },
                "Noćno treniranje modela preporuka nije uspjelo.",
                stoppingToken);
        }
    }

    /// <summary>Time until the next occurrence of the configured hour. Always strictly positive:
    /// at exactly the configured hour this returns a full day rather than 0, so the loop can't spin
    /// through repeated same-minute retrains.</summary>
    private TimeSpan TimeUntilNextRun()
    {
        var now = _timeProvider.GetLocalNow();
        var hour = Math.Clamp(_options.TrainAtHour, 0, 23);

        var next = new DateTimeOffset(now.Year, now.Month, now.Day, hour, 0, 0, now.Offset);
        if (next <= now)
            next = next.AddDays(1);

        return next - now;
    }

    /// <summary>A failed training run must never take the web host down with it — Catalog's real
    /// job is serving the product catalog, and it does that fine with a stale model or none.</summary>
    private async Task RunSafely(Func<IRecommendationService, CancellationToken, Task> action, string failureMessage, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await action(scope.ServiceProvider.GetRequiredService<IRecommendationService>(), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down — not a failure.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", failureMessage);
        }
    }
}
