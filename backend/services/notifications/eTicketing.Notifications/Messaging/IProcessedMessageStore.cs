namespace eTicketing.Notifications.Messaging;

/// <summary>Remembers which deliveries have already produced their email, so a redelivery of the
/// same event does not send it twice. Keyed by <see cref="DeliveryDedupeKey"/>. See
/// <see cref="DeduplicatingDeliveryHandler"/> for when each method is called and why the order
/// matters.</summary>
public interface IProcessedMessageStore
{
    Task<bool> IsProcessedAsync(string key, CancellationToken ct = default);

    Task MarkProcessedAsync(string key, CancellationToken ct = default);
}
