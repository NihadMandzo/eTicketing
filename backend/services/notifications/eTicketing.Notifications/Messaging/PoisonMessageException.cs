namespace eTicketing.Notifications.Messaging;

/// <summary>Thrown by NotificationDispatcher for failures that retrying can never fix — bad
/// JSON, an unrecognized routing key. The consumer catches this specifically and routes the
/// message straight to the dead-letter queue instead of the retry ladder (see
/// RabbitMqConsumerService), so a malformed message doesn't loop forever.</summary>
public sealed class PoisonMessageException : Exception
{
    public PoisonMessageException(string message) : base(message) { }
    public PoisonMessageException(string message, Exception innerException) : base(message, innerException) { }
}
