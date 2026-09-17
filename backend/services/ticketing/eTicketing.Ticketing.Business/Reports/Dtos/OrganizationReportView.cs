namespace eTicketing.Ticketing.Business.Reports;

/// <summary>Which column set the Organizations tab should render. SuperAdmin sees the financial
/// view (tickets, revenue, growth), Admin the operational one (published/pending/no-image),
/// mirroring the two-column-set split in docs/Design/Reports.dc.html.</summary>
public enum OrganizationReportView
{
    Financial,
    Operational
}
