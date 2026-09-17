namespace eTicketing.Ticketing.Business.Reports;

/// <summary>
/// One row of the Prodaja tab's per-organization breakdown. Populated only for a platform-wide
/// caller — an organizer's report is already scoped to a single organization, so breaking it down
/// by organization would be one row restating the headline totals.
///
/// <para>Lists the organizations that <i>sold something in the range</i>, not every organization
/// on the platform: this is a breakdown of the revenue reported above it, so its rows must sum to
/// that revenue. The Organizacije tab is the one that answers "how is every organization doing",
/// and it drives its row set from Catalog's product stats for exactly that reason.</para>
/// </summary>
public sealed record SalesByOrganizationRow(
    Guid OrganizationId,
    string Name,
    int Sold,
    decimal Revenue,
    decimal AveragePrice,
    // Share of GrossRevenue, so the column sums to 100% across the rows.
    decimal SharePercent);
