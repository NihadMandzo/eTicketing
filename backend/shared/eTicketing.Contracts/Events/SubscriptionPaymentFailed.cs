namespace eTicketing.Contracts.Events;

/// <summary>
/// eTicketing.Payment → eTicketing.Ticketing. A renewal charge failed. The subscription becomes
/// PastDue and no ticket is minted, but nothing is released yet: the provider is still retrying its
/// own dunning schedule, and only when that is exhausted does it fire the cancellation below.
/// </summary>
public record SubscriptionPaymentFailed(
    string ProviderSubscriptionId,
    string Reason,
    DateTime FailedAt);
