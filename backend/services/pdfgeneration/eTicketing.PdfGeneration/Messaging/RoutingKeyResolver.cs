namespace eTicketing.PdfGeneration.Messaging;

/// <summary>Recovers the event's original routing key from a delivery.
///
/// A retried message does not arrive with the routing key it was published under: the retry
/// republish goes through the default exchange (routing key == queue name), and the retry queue's
/// TTL expiry then dead-letters it back to <see cref="RetryQueueNames.Main"/> under
/// <c>x-dead-letter-routing-key</c>. By the time the second attempt is delivered,
/// <c>delivery.RoutingKey</c> reads "pdfgeneration.tickets" — the queue name — not
/// <c>ticket.purchased</c>, so the dispatcher would reject a perfectly valid message as poison and
/// the buyer would never receive their ticket.
///
/// RepublishAsync therefore stashes the original key in the AMQP <c>Type</c> property, which
/// survives both hops untouched. Same approach as eTicketing.Catalog's consumer.</summary>
public static class RoutingKeyResolver
{
    public static string Resolve(string? originalRoutingKey, string deliveryRoutingKey)
        => string.IsNullOrWhiteSpace(originalRoutingKey) ? deliveryRoutingKey : originalRoutingKey;
}
