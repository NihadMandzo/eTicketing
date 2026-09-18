namespace eTicketing.Ticketing.Data.Repositories;

/// <summary>Gate usage for one product over the whole reporting range.</summary>
public record ProductRedemptionFacts(Guid ProductId, int Sold, int CheckedIn, int Printed);
