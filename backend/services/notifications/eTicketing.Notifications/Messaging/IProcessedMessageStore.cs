namespace eTicketing.Notifications.Messaging;

/// <summary>
/// Remembers which deliveries have already produced their email, so a redelivery of the same event
/// does not send it twice. Keyed by <see cref="DeliveryDedupeKey"/>.
///
/// <para>The claim is what makes this safe with more than one replica: checking and then marking
/// would let two consumers both see "not processed" and both send. See
/// <see cref="DeduplicatingDeliveryHandler"/> for the order the three calls are made in.</para>
/// </summary>
public interface IProcessedMessageStore
{
    /// <summary>Takes the key if it is free, and otherwise reports what is holding it. The lease
    /// bounds how long a claim survives a consumer that dies mid-send.</summary>
    Task<DeliveryClaim> TryClaimAsync(string key, TimeSpan lease, CancellationToken ct = default);

    /// <summary>Turns this caller's claim into a lasting record that the email was sent.</summary>
    Task MarkProcessedAsync(string key, CancellationToken ct = default);

    /// <summary>Gives up a claim whose send failed, so the retry does not have to wait out the
    /// lease. Only ever releases a claim, never a processed record.</summary>
    Task ReleaseClaimAsync(string key, CancellationToken ct = default);
}
