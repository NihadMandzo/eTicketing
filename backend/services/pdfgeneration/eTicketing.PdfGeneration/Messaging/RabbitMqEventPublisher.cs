using System.Text;
using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.PdfGeneration.Options;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace eTicketing.PdfGeneration.Messaging;

/// <summary>
/// Mirrors every other service's copy, with one deliberate difference: this one RETHROWS on
/// failure instead of logging and swallowing.
///
/// Elsewhere the publish is a side effect after the real work is already committed, so eating the
/// exception is right — the purchase must not fail because the broker hiccuped. Here the publish IS
/// the work: TicketPdfReady is what triggers the buyer's email and the Confirmed → Ready flip. A
/// swallowed failure would leave PDFs sitting in blob storage that nobody is ever told about. Letting
/// it throw hands the message back to the consumer's retry ladder, which is exactly what should
/// happen.
/// </summary>
public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly RabbitMqOptions _options;

    public RabbitMqEventPublisher(IOptions<RabbitMqOptions> options) => _options = options.Value;

    public async Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default)
    {
        // See RabbitMqConsumerService for why this can't be a plain HostName assignment.
        var factory = Uri.TryCreate(_options.Host, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("amqp")
            ? new ConnectionFactory { Uri = uri }
            : new ConnectionFactory { HostName = _options.Host };
        using var connection = await factory.CreateConnectionAsync(ct);
        using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync(EventNames.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await channel.BasicPublishAsync(EventNames.Exchange, routingKey, body, ct);
    }
}
