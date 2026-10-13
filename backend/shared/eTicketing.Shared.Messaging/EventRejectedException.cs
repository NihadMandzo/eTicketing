namespace eTicketing.Shared.Messaging;

/// <summary>
/// The broker received this one message and refused it — it is unroutable (no queue is bound to
/// its routing key) or the broker negatively acknowledged it. A statement about the message, not
/// about the broker: the next message may well go through.
///
/// <para>Thrown by <see cref="RabbitMqEventPublisher"/> in place of the client's own
/// <c>PublishException</c>, so <see cref="OutboxDispatcher{TContext}"/> can tell "skip this row and
/// carry on" apart from "the broker is down, stop the pass" without depending on RabbitMQ types.</para>
/// </summary>
public sealed class EventRejectedException : Exception
{
    public EventRejectedException(string routingKey, Exception inner)
        : base($"Broker je odbio poruku '{routingKey}': {inner.Message}", inner)
    {
        RoutingKey = routingKey;
    }

    public string RoutingKey { get; }
}
