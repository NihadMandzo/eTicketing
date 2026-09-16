namespace eTicketing.Catalog.Data.Repositories;

/// <summary>Aggregate counts behind the back-office screen's stat cards.</summary>
public record InteractionStats(int TotalInteractions, int ViewCount, int PurchaseCount, int DistinctUsers, int DistinctProducts);
