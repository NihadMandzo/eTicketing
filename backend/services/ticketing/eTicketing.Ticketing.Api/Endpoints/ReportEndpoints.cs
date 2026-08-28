using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Ticketing.Business.Reports;

namespace eTicketing.Ticketing.Api.Endpoints;

/// <summary>
/// GET /reports/* — the desktop back-office's Izvještaji screen.
///
/// The policies here are the coarse gate only. "Organizer" admits every Org*Admin *and* platform
/// staff (see AuthorizationPolicyExtensions), which is deliberately wider than any single report:
/// the actual per-role matrix (Admin sees no sales, OrganizationAdmin sees no check-ins, only
/// SuperAdmin sees the financial organization columns) lives in ReportService.Authorize, because
/// it is a business rule about which report belongs to which job, not an endpoint-reachability
/// rule. Organizations is the one exception where a policy alone says it: no organizer of any
/// kind may see other organizations, so PlatformStaff is exactly right there.
/// </summary>
public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/reports").WithTags("Reports");

        group.MapGet("/sales", GetSales).RequireAuthorization("Organizer").WithValidation<ReportQuery>();
        group.MapGet("/products", GetProducts).RequireAuthorization("Organizer").WithValidation<ReportQuery>();
        group.MapGet("/redemption", GetRedemption).RequireAuthorization("Organizer").WithValidation<ReportQuery>();
        group.MapGet("/organizations", GetOrganizations).RequireAuthorization("PlatformStaff").WithValidation<ReportQuery>();

        group.MapGet("/export", Export).RequireAuthorization("Organizer").WithValidation<ReportExportQuery>();
    }

    private static async Task<IResult> GetSales(
        [AsParameters] ReportQuery query, IReportService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetSalesAsync(query, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetProducts(
        [AsParameters] ReportQuery query, IReportService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetProductsAsync(query, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetRedemption(
        [AsParameters] ReportQuery query, IReportService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetRedemptionAsync(query, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetOrganizations(
        [AsParameters] ReportQuery query, IReportService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetOrganizationsAsync(query, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Export(
        [AsParameters] ReportExportQuery query, IReportPdfService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.ExportAsync(query, http.User, ct);
        if (!result.IsSuccess)
        {
            return result.ToHttpResult();
        }

        // "attachment", unlike the buyer's ticket PDF (which is served inline so it opens in a
        // phone's viewer at the gate). A report is something the organizer saves and sends on, and
        // the desktop client writes it straight to a file the user picked.
        var pdf = result.Value!;
        http.Response.Headers.ContentDisposition = $"attachment; filename=\"{pdf.FileName}\"";
        return Results.File(pdf.Content, "application/pdf", pdf.FileName);
    }
}
