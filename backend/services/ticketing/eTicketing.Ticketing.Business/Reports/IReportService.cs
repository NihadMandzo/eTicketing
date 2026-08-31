using System.Security.Claims;
using eTicketing.Contracts.Results;

namespace eTicketing.Ticketing.Business.Reports;

/// <summary>
/// Backs GET /reports/*. Every method takes the caller's principal and enforces two separate
/// things from it:
///
/// 1. <b>Which tabs the role may see at all</b> — the matrix in docs/Design/Reports.dc.html:
///    SuperAdmin gets all four, Admin only products + organizations, OrganizationSuperAdmin
///    sales + products + redemption, OrganizationAdmin only sales + products (and no export).
///    The desktop screen renders the same matrix, but hiding a tab is not authorization
///    (.claude/rules/21-frontend-desktop.md) — a hand-rolled request for a forbidden tab must be
///    refused here.
/// 2. <b>What data is in scope</b> — PlatformStaff see the whole platform, Org* roles only their
///    own <c>organizationId</c> claim. No caller can widen its own scope: the organization is read
///    from the token, never from the request.
/// </summary>
public interface IReportService
{
    /// <summary>Prodaja — revenue over time, headline totals and cancellations. Forbidden to
    /// Admin, whose remit is operational rather than financial.</summary>
    Task<Result<SalesReportResponse>> GetSalesAsync(ReportQuery query, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Učinak proizvoda — per-product sales, occupancy and revenue. Available to every
    /// staff role, scoped to the caller's organization for Org* roles.</summary>
    Task<Result<ProductReportResponse>> GetProductsAsync(ReportQuery query, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Iskorištenost karata — gate check-ins, no-shows and arrival times. Forbidden to
    /// Admin and OrganizationAdmin.</summary>
    Task<Result<RedemptionReportResponse>> GetRedemptionAsync(ReportQuery query, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Organizacije — platform staff only. SuperAdmin gets the financial column set,
    /// Admin the operational one; see <see cref="OrganizationReportView"/>.</summary>
    Task<Result<OrganizationReportResponse>> GetOrganizationsAsync(ReportQuery query, ClaimsPrincipal user, CancellationToken ct = default);
}
