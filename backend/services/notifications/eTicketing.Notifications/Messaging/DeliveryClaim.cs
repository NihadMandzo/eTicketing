namespace eTicketing.Notifications.Messaging;

/// <summary>What <see cref="IProcessedMessageStore.TryClaimAsync"/> found for a delivery's dedupe
/// key, and therefore what the handler should do with it.</summary>
public enum DeliveryClaim
{
    /// <summary>Nobody else holds this key — this caller sends the email.</summary>
    Claimed,

    /// <summary>The email already went out. Skip it.</summary>
    AlreadyProcessed,

    /// <summary>Another consumer is sending it right now, and its claim has not expired. Come back
    /// later rather than sending a second copy alongside it.</summary>
    InProgress,
}
