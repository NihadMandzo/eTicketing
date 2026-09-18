namespace eTicketing.Ticketing.Data.Repositories;

/// <summary>
/// One buyer's behaviour over the segmentation window — the raw RFM inputs, before any
/// normalization or clustering, which both happen in the Business layer.
///
/// <paramref name="Orders"/> counts distinct OrderId values, not tickets: someone who bought three
/// seats to one show in one transaction is one purchase occasion, and counting them as three would
/// make party-buyers look like frequent customers.
/// </summary>
public record BuyerFacts(
    Guid UserId, int Orders, int Tickets, decimal Spend, DateTime FirstPurchaseUtc, DateTime LastPurchaseUtc);
