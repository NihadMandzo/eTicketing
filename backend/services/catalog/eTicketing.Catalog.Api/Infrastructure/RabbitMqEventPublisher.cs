using System.Text;
using System.Text.Json;
using eTicketing.Catalog.Business.Products;
using eTicketing.Contracts.Events;
using RabbitMQ.Client;

namespace eTicketing.Catalog.Api.Infrastructure;

/// <summary>Mirrors eTicketing.Identity.Api's and eTicketing.Ticketing.Api's copies exactly —
/// duplicated per-service rather than shared/promoted to Contracts, the convention Identity
/// established.</summary>
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
            var factory = new ConnectionFactory { HostName = _configuration["RabbitMq:Host"] ?? "localhost" };
            using var connection = await factory.CreateConnectionAsync(ct);
            using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

            await channel.ExchangeDeclareAsync(EventNames.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            await channel.BasicPublishAsync(EventNames.Exchange, routingKey, body, ct);
        }
        catch (Exception ex)
        {
            // Publishing must never fail the edit itself — the product row is already committed by
            // the time this runs, and an organizer whose save "failed" would just save again and
            // change nothing. But a dropped product.updated silently leaves every ticket holder
            // uninformed about a moved date or venue, so this is an error, not a warning.
            _logger.LogError(ex, "Neuspjela objava eventa {RoutingKey} na RabbitMQ.", routingKey);
        }
    }
}
