using eTicketing.Contracts.Events;
using RabbitMQ.Client;

namespace eTicketing.PdfGeneration.Messaging;

/// <summary>Declares this service's queue layout: the main subscription queue bound to
/// <c>ticket.purchased</c>, the 5 backoff-tier retry queues, and the dead-letter queue. Idempotent
/// — safe to call on every startup. Mirrors eTicketing.Notifications' topology exactly, with this
/// service's own queue names.</summary>
public static class RabbitMqTopology
{
    private static readonly string[] RoutingKeys = [EventNames.TicketPurchased];

    public static async Task DeclareAsync(IChannel channel, CancellationToken ct = default)
    {
        await channel.ExchangeDeclareAsync(EventNames.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

        await channel.QueueDeclareAsync(RetryQueueNames.Main, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        foreach (var routingKey in RoutingKeys)
        {
            await channel.QueueBindAsync(RetryQueueNames.Main, EventNames.Exchange, routingKey, cancellationToken: ct);
        }

        foreach (var (queueName, ttlMilliseconds) in RetryQueueNames.Tiers)
        {
            var arguments = new Dictionary<string, object?>
            {
                ["x-message-ttl"] = ttlMilliseconds,
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = RetryQueueNames.Main,
            };
            await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, arguments: arguments, cancellationToken: ct);
        }

        // No TTL — poison messages sit here until manually inspected/replayed via the RabbitMQ
        // management UI, never auto-retried.
        await channel.QueueDeclareAsync(RetryQueueNames.DeadLetter, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
    }
}
