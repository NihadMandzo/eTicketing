namespace eTicketing.Notifications.Messaging;

/// <summary>
/// Thrown when another consumer holds the claim on this delivery's dedupe key — it is sending the
/// email at this moment.
///
/// <para>Deliberately an exception rather than a quiet skip: skipping would be a guess that the other
/// consumer succeeds, and if it does not, nobody ever sends the email. Thrown, the delivery takes
/// RabbitMqConsumerService's ordinary retry ladder; by the next attempt the other consumer has
/// normally finished and marked the key processed, so the retry recognises it as a duplicate and
/// skips it for the right reason.</para>
/// </summary>
public sealed class DeliveryInProgressException : Exception
{
    public DeliveryInProgressException(string message) : base(message) { }
}
