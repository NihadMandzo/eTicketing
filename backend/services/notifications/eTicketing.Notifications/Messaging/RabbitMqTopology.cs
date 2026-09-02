using eTicketing.Contracts.Events;
using RabbitMQ.Client;

namespace eTicketing.Notifications.Messaging;

/// <summary>Declares the full queue/exchange layout this service depends on: the main
/// subscription queue, the 5 backoff-tier retry queues, and the dead-letter queue. Idempotent —
/// safe to call on every startup (RabbitMQ no-ops a declare that already matches).
///
/// Each retry queue dead-letters back onto <see cref="RetryQueueNames.Main"/> via the RabbitMQ
/// default exchange (routing purely by queue name) once its TTL expires — no custom exchange
/// needed for that hop. See RetryQueueNames for the full ladder.</summary>
public static class RabbitMqTopology
{
    private static readonly string[] RoutingKeys =
    [
        EventNames.VerificationEmailRequested,
        EventNames.OrganizationCreated,
        EventNames.OrganizationAdminDeleted,
        EventNames.PasswordResetRequested,
        EventNames.AdminPasswordChanged,
        // Chained behind eTicketing.PdfGeneration, not published directly by Ticketing: the
        // confirmation email carries the ticket PDFs, so it can only be sent once they exist.
        EventNames.TicketPdfReady,
        EventNames.ProductChanged,
        EventNames.ProductDeletedNotification,
    ];

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
