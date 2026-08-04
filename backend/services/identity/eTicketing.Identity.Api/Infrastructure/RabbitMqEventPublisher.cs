using System.Text;
using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.Identity.Business.Auth;
using RabbitMQ.Client;

namespace eTicketing.Identity.Api.Infrastructure;

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
            // Objava eventa ne smije oboriti glavni tok (npr. registraciju) — samo se loguje.
            // Notifications servis još ne postoji do Sprinta 4, pa je ovo očekivano dok se ne poveže consumer.
            _logger.LogWarning(ex, "Neuspjela objava eventa {RoutingKey} na RabbitMQ.", routingKey);
        }
    }
}
