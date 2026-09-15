using System.Security.Claims;
using eTicketing.Contracts.Results;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;

namespace eTicketing.Ticketing.Business.Reports;

/// <summary>The outcome of the role/tab matrix check: either an <see cref="Error"/> to return,
/// or the organization the caller's data must be scoped to (null = whole platform).</summary>
internal readonly record struct ReportAccess(Error? Error, Guid? OrganizationId)
{
    public static ReportAccess Denied(Error error) => new(error, null);
    public static ReportAccess Platform() => new(null, null);
    public static ReportAccess Organization(Guid id) => new(null, id);
}

/// <summary>
/// The single place the report matrix lives. Written as an explicit switch over role × tab
/// rather than as a set of authorization policies because the split is not "can you reach this
/// endpoint" but "which of the reports does your job involve" — Admin and SuperAdmin share every
/// ASP.NET policy in this codebase yet see different tabs here.
///
/// Lives in its own class rather than inside ReportService because AnalyticsService answers the
/// same question for the AI Uvidi tab. One copy, so the two can never drift into disagreeing
/// about who may see what.
/// </summary>
internal static class ReportAuthorization
{
    public static ReportAccess Authorize(ClaimsPrincipal user, ReportTab tab)
    {
        var forbidden = Error.Unauthorized("report.forbidden", "Nemate pristup ovom izvještaju.");

        switch (user.GetRole())
        {
            case "SuperAdmin":
                return ReportAccess.Platform();

            case "Admin":
                // Platform-wide reach, operational remit: no revenue reports, no gate statistics.
                // Insights is a revenue report at heart (it forecasts and segments money), so it
                // sits with Sales on the far side of that line.
                return tab is ReportTab.Products or ReportTab.Organizations
                    ? ReportAccess.Platform()
                    : ReportAccess.Denied(forbidden);

            case "OrganizationSuperAdmin":
            case "OrganizationAdmin":
            {
                if (tab == ReportTab.Organizations)
                    return ReportAccess.Denied(forbidden);

                // OrganizationAdmin is the narrowest role: sales, products and insights for their
                // own organization only, no gate statistics and no export (see IReportPdfService).
                if (tab == ReportTab.Redemption && user.IsInRole("OrganizationAdmin"))
                    return ReportAccess.Denied(forbidden);

                var organizationId = user.GetOrganizationId();
                if (organizationId is null)
                {
                    // An Org* token with no organizationId claim is a malformed session, not a
                    // permission question — refuse rather than silently widening to the platform.
                    return ReportAccess.Denied(Error.Unauthorized(
                        "report.no_organization", "Vaš nalog nije povezan ni sa jednom organizacijom."));
                }

                return ReportAccess.Organization(organizationId.Value);
            }

            default:
                return ReportAccess.Denied(forbidden);
        }
    }
}
