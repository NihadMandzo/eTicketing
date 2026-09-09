using System.Text;
using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.Payment.Business.Payments.Webhooks;
using RabbitMQ.Client;

namespace eTicketing.Payment.Api.Infrastructure;

/// <summary>Duplicated per-service rather than shared/promoted to Contracts, the convention
/// Identity established for IEventPublisher. No longer identical to
/// eTicketing.Ticketing.Api/Infrastructure/RabbitMqEventPublisher.cs: this one rethrows on a
/// failed publish (see the catch block below), so a lost subscription.renewed becomes a
/// retryable 5xx instead of a silently-lost ticket. Ticketing's still swallows and logs --
/// publishing there must never break an already-committed purchase.</summary>
public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqEventPublisher> _logger;

    public RabbitMqEventPublisher(IConfiguration configuration, ILogger<RabbitMqEventPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default)
    {
        try
        {
            // RabbitMq:Host doubles as either a bare hostname (local docker-compose broker) or a
            // full amqp(s)://user:pass@host/vhost connection string (self-hosted RabbitMQ in Azure
            // needs real credentials — the default guest user only accepts localhost connections) —
            // HostName alone can't parse the latter, it would just try to DNS-resolve the whole URI.
            var host = _configuration["RabbitMq:Host"] ?? "localhost";
            var factory = Uri.TryCreate(host, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("amqp")
                ? new ConnectionFactory { Uri = uri }
                : new ConnectionFactory { HostName = host };
            using var connection = await factory.CreateConnectionAsync(ct);
            using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

            await channel.ExchangeDeclareAsync(EventNames.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            await channel.BasicPublishAsync(EventNames.Exchange, routingKey, body, ct);
        }
        catch (Exception ex)
        {
            // Deliberately rethrown rather than swallowed. A dropped subscription.renewed means a
            // buyer who WAS charged never gets that month's parking ticket, and if this returned
            // normally the webhook would answer 2xx and Stripe would never redeliver -- the event
            // would be lost for good. Letting it out makes the webhook a 5xx, which is precisely
            // the signal that gets the delivery retried. Safe because StripeWebhookService commits
            // nothing until after the publish returns, so a retry re-processes from a clean slate.
            _logger.LogError(ex, "Neuspjela objava eventa {RoutingKey} na RabbitMQ.", routingKey);
            throw;
        }
    }
}
