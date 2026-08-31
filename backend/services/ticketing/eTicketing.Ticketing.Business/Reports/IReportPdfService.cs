using System.Security.Claims;
using eTicketing.Contracts.Results;

namespace eTicketing.Ticketing.Business.Reports;

/// <summary>
/// Backs GET /reports/export. Renders whichever report the caller is looking at as a PDF.
///
/// It fetches through <see cref="IReportService"/> rather than querying repositories itself, so
/// the tab matrix and the organization scoping are enforced in exactly one place and the exported
/// figures are by construction the same ones the screen showed. On top of that it applies the one
/// rule that is specific to exporting: OrganizationAdmin cannot export at all (the design hides
/// the button for that role — this is the half that actually enforces it).
/// </summary>
public interface IReportPdfService
{
    Task<Result<ReportPdfResult>> ExportAsync(ReportExportQuery query, ClaimsPrincipal user, CancellationToken ct = default);
}
