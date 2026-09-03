using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Ticketing.Business.Analytics;

namespace eTicketing.Ticketing.Api.Endpoints;

/// <summary>
/// GET /reports/insights — the desktop back-office's AI Uvidi tab.
///
/// Mapped into the same /reports group as the four descriptive reports, so it needs no gateway
/// route of its own: /api/reports/** already forwards here. The "Organizer" policy is the same
/// coarse gate the other reports use; which roles actually see this tab is decided by
/// ReportAuthorization, because it is a business rule about whose job involves revenue analysis
/// rather than an endpoint-reachability rule (see ReportEndpoints).
///
/// One endpoint rather than one per block: the screen loads exactly one tab per request, and
/// splitting forecast/anomalies/segments would turn one tab switch into three round trips over the
/// same range while re-running the same three report queries each time.
/// </summary>
public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/reports").WithTags("Reports");

        group.MapGet("/insights", GetInsights)
            .RequireAuthorization("Organizer")
            .WithValidation<InsightsQuery>();
    }

    private static async Task<IResult> GetInsights(
        [AsParameters] InsightsQuery query, IAnalyticsService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetInsightsAsync(query, http.User, ct);
        return result.ToHttpResult();
    }
}
