namespace eTicketing.Ticketing.Data.Repositories;

/// <summary>Sales for one product over the whole reporting range.</summary>
public record ProductSalesFacts(Guid ProductId, int Sold, decimal Revenue, int Cancelled, decimal CancelledAmount);
