using System.Security.Claims;
using eTicketing.Contracts.Results;

namespace eTicketing.Ticketing.Business.Analytics;

/// <summary>
/// Backs GET /reports/insights — the AI Uvidi tab.
///
/// Enforces the same two things every report does, through the same shared matrix
/// (ReportAuthorization): which roles may see this tab at all, and which organization's data is in
/// scope. Insights sits with Sales on that matrix — it forecasts and segments money, so Admin,
/// whose remit is operational, is refused exactly as it is for Prodaja.
/// </summary>
public interface IAnalyticsService
{
    Task<Result<AnalyticsInsightsResponse>> GetInsightsAsync(
        InsightsQuery query, ClaimsPrincipal user, CancellationToken ct = default);
}
