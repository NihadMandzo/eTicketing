namespace eTicketing.Ticketing.Data.Repositories;

/// <summary>Sales for one organization over the whole reporting range.</summary>
public record OrganizationSalesFacts(Guid OrganizationId, int Sold, decimal Revenue);
