namespace eTicketing.Ticketing.Data.Repositories;

/// <summary>Current sold count for one product, unscoped by purchase date.</summary>
public record ProductSoldCount(Guid ProductId, int Sold);
