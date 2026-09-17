namespace eTicketing.Contracts.Events;

/// <summary>
/// eTicketing.Payment → eTicketing.Ticketing. The subscription has ended for good, either because
/// the buyer cancelled and the paid-for period ran out, or because dunning gave up. This is what
/// frees the parking space back to its sector's capacity.
/// </summary>
public record SubscriptionCancelled(
    string ProviderSubscriptionId,
    DateTime CancelledAt);
