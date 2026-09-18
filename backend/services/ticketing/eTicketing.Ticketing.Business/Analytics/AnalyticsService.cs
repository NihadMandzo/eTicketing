using System.Security.Claims;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.Analytics.Anomalies;
using eTicketing.Ticketing.Business.Analytics.Forecasting;
using eTicketing.Ticketing.Business.Analytics.Insights;
using eTicketing.Ticketing.Business.Analytics.Narrative;
using eTicketing.Ticketing.Business.Analytics.Segmentation;
using eTicketing.Ticketing.Business.Reports;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eTicketing.Ticketing.Business.Analytics;

/// <inheritdoc cref="IAnalyticsService"/>
public class AnalyticsService : IAnalyticsService
{
    /// <summary>
    /// How far back segmentation looks, regardless of the range on screen.
    ///
    /// Recency and frequency over a week are not descriptions of a customer, they are descriptions
    /// of a week — everybody looks dormant in a range that ends on Tuesday. A year is long enough
    /// for "kupuju često" to mean something and short enough that a buyer who left two years ago is
    /// not still counted as this organization's audience. The response says which window was used
    /// so the mismatch with the selected range reads as a decision.
    /// </summary>
    private const int SegmentationWindowDays = 365;

    private const string SegmentationWindowLabel = "posljednjih 12 mjeseci";

    private readonly IReportService _reports;
    private readonly ITicketRepository _tickets;
    private readonly ISalesForecaster _forecaster;
    private readonly IAnomalyDetector _anomalies;
    private readonly IAudienceSegmenter _segmenter;
    private readonly IInsightGenerator _insights;
    private readonly INarrativeWriter _narrative;
    private readonly PlatformClock _clock;
    private readonly IMemoryCache _cache;
    private readonly InsightsOptions _options;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        IReportService reports,
        ITicketRepository tickets,
        ISalesForecaster forecaster,
        IAnomalyDetector anomalies,
        IAudienceSegmenter segmenter,
        IInsightGenerator insights,
        INarrativeWriter narrative,
        PlatformClock clock,
        IMemoryCache cache,
        IOptions<InsightsOptions> options,
        ILogger<AnalyticsService> logger)
    {
        _reports = reports;
        _tickets = tickets;
        _forecaster = forecaster;
        _anomalies = anomalies;
        _segmenter = segmenter;
        _insights = insights;
        _narrative = narrative;
        _clock = clock;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<AnalyticsInsightsResponse>> GetInsightsAsync(
        InsightsQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var access = ReportAuthorization.Authorize(user, ReportTab.Insights);
        if (access.Error is not null)
            return Result<AnalyticsInsightsResponse>.Failure(access.Error);

        // The role is part of the key, not only the organization: an OrganizationAdmin's response
        // legitimately omits the gate statistics an OrganizationSuperAdmin of the same organization
        // sees, and serving one the other's cached copy would be an authorization bug wearing a
        // performance optimization as a disguise.
        var cacheKey = (
            nameof(AnalyticsService),
            access.OrganizationId,
            user.GetRole(),
            query.From,
            query.To,
            query.Horizon);

        if (_cache.TryGetValue(cacheKey, out AnalyticsInsightsResponse? cached) && cached is not null)
            return Result<AnalyticsInsightsResponse>.Success(cached);

        var response = await BuildAsync(query, access.OrganizationId, user, ct);
        if (response.IsFailure)
            return response;

        // One cache entry covers the narrative too. A separate, longer-lived narrative cache was
        // considered and dropped: it buys one avoided model call every ten minutes and costs a
        // second key that can go stale against the figures it describes, which is the one failure
        // mode a generated summary must not have.
        _cache.Set(cacheKey, response.Value!, TimeSpan.FromMinutes(Math.Max(1, _options.CacheMinutes)));

        return response;
    }

    private async Task<Result<AnalyticsInsightsResponse>> BuildAsync(
        InsightsQuery query, Guid? organizationId, ClaimsPrincipal user, CancellationToken ct)
    {
        var range = ReportRange.Create(query, _clock);

        // The three descriptive reports are fetched rather than recomputed. They already carry the
        // scope label, the cancellation rate, the occupancy and the channel split that half the
        // insight rules read, and re-deriving them here is exactly how an insight card ends up
        // contradicting the tile the user saw on the previous tab.
        var sales = await _reports.GetSalesAsync(new ReportQuery { From = query.From, To = query.To }, user, ct);
        if (sales.IsFailure)
            return Result<AnalyticsInsightsResponse>.Failure(sales.Error);

        var products = await _reports.GetProductsAsync(new ReportQuery { From = query.From, To = query.To }, user, ct);
        if (products.IsFailure)
            return Result<AnalyticsInsightsResponse>.Failure(products.Error);

        // Redemption is the one report a caller of this tab may legitimately be refused
        // (OrganizationAdmin). Its refusal is absorbed rather than propagated: the no-show rule
        // simply does not fire, and the rest of the tab is data that role is entitled to.
        var redemption = await _reports.GetRedemptionAsync(new ReportQuery { From = query.From, To = query.To }, user, ct);

        var daily = ReportSeries.ToLocalDays(
            await _tickets.GetHourlySalesAsync(organizationId, range.FromUtc, range.ToUtcExclusive, ct), _clock);
        var series = ReportSeries.ToDailySeries(range, daily);

        var forecast = _forecaster.Forecast(series, query.Horizon);
        var anomalies = _anomalies.Detect(series);
        var segments = await SegmentAsync(query, organizationId, ct);

        var generated = _insights.Generate(new InsightContext(
            forecast, anomalies, segments, sales.Value!, products.Value!, redemption.IsSuccess ? redemption.Value : null));

        var narrative = await NarrateAsync(
            new NarrativeContext(sales.Value!.Scope, range.ToPeriod(), sales.Value!, forecast, anomalies, segments, generated),
            ct);

        return new AnalyticsInsightsResponse(
            Period: range.ToPeriod(),
            Scope: sales.Value!.Scope,
            Forecast: forecast,
            Anomalies: anomalies,
            Segments: segments,
            Insights: generated,
            Narrative: narrative);
    }

    private async Task<SegmentBlock> SegmentAsync(InsightsQuery query, Guid? organizationId, CancellationToken ct)
    {
        var window = ReportRange.Create(query.To.AddDays(-(SegmentationWindowDays - 1)), query.To, _clock);
        var buyers = await _tickets.GetBuyerFactsAsync(organizationId, window.FromUtc, window.ToUtcExclusive, ct);

        return _segmenter.Segment(buyers, query.To, SegmentationWindowLabel);
    }

    /// <summary>
    /// The last, optional step.
    ///
    /// A writer is contractually forbidden from throwing, but this catch stays anyway: the whole
    /// point of the narrative being optional is that nothing about it can cost the user the report,
    /// and that guarantee should not depend on every future implementation remembering the rule.
    /// Same stance as RecommendationTrainingHostedService.RunSafely — a failed AI step degrades the
    /// feature it belongs to and nothing else.
    /// </summary>
    private async Task<string?> NarrateAsync(NarrativeContext context, CancellationToken ct)
    {
        try
        {
            return await _narrative.WriteAsync(context, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Generisanje AI sažetka nije uspjelo — izvještaj se vraća bez njega.");
            return null;
        }
    }
}
