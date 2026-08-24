namespace eTicketing.PdfGeneration.Messaging;

/// <summary>Marks a failure no retry can fix — malformed JSON, an unknown routing key, or an order
/// whose product no longer exists. Mirrors eTicketing.Notifications' exception of the same name so
/// the two consumers read the same way.</summary>
public class PoisonMessageException : Exception
{
    public PoisonMessageException(string message) : base(message) { }

    public PoisonMessageException(string message, Exception innerException) : base(message, innerException) { }
}
