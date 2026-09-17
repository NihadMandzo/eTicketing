using eTicketing.Ticketing.Business.Reports;
using static eTicketing.Ticketing.Business.Reports.ReportFormatting;

namespace eTicketing.Ticketing.Business.Analytics.Narrative;

/// <summary>
/// Writes the Bosnian executive summary above the insight cards.
///
/// <para><b>Returning null is a normal outcome, not an error.</b> No provider configured, the model
/// server down, a timeout, a malformed response — all of them answer null and the tab renders
/// without a summary. Implementations must not throw: the deterministic insights are the feature
/// and they are already computed by the time this is called, so failing the request at this point
/// would throw away good work over an optional flourish.</para>
/// </summary>
public interface INarrativeWriter
{
    Task<string?> WriteAsync(NarrativeContext context, CancellationToken ct = default);
}
