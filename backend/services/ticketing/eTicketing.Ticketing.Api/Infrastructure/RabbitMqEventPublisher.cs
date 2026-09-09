using System.Text;
using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.Ticketing.Business.Purchases;
using RabbitMQ.Client;

namespace eTicketing.Ticketing.Api.Infrastructure;

/// <summary>Mirrors eTicketing.Identity.Api/Infrastructure/RabbitMqEventPublisher.cs exactly —
/// duplicated per-service rather than shared/promoted to Contracts, same convention Identity
/// already established for IEventPublisher.</summary>
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
            // See TicketingRabbitMqConsumerService for why this can't be a plain HostName assignment.
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
            // Publishing must never break the purchase itself — the Ticket is already committed by
            // the time this runs. But a dropped ticket.purchased event silently skips the
            // confirmation email/PDF for a real paying customer, so this is an error, not a warning.
            _logger.LogError(ex, "Neuspjela objava eventa {RoutingKey} na RabbitMQ.", routingKey);
        }
    }
}
