using System.Text;
using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.Payment.Business.Payments.Webhooks;
using RabbitMQ.Client;

namespace eTicketing.Payment.Api.Infrastructure;

/// <summary>Mirrors eTicketing.Ticketing.Api/Infrastructure/RabbitMqEventPublisher.cs exactly --
/// duplicated per-service rather than shared/promoted to Contracts, the convention Identity
/// established for IEventPublisher.</summary>
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
            // A dropped subscription.renewed means a buyer who WAS charged never gets that month's
            // parking ticket, and nothing else in the system will notice -- Stripe already has its
            // 200 and will not redeliver. Loud error, and the reason the manual test plan checks the
            // renewal end to end rather than trusting the publish.
            _logger.LogError(ex, "Neuspjela objava eventa {RoutingKey} na RabbitMQ.", routingKey);
        }
    }
}
