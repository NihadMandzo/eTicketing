using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static eTicketing.Shared.TicketPdf.TicketTheme;

namespace eTicketing.Shared.TicketPdf;

internal static class ReportDocumentExtensions
{
    /// <summary>Numeric columns read right-aligned; the name column that opens every table reads
    /// left. Applied in one place so the header, the body and the totals row can never disagree
    /// about a column's alignment.</summary>
    public static IContainer AlignmentFor(this IContainer container, ReportPdfColumn column)
        => column.RightAligned ? container.AlignRight() : container.AlignLeft();
}
