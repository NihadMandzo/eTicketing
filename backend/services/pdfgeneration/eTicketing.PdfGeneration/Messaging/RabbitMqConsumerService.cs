using eTicketing.PdfGeneration.Options;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace eTicketing.PdfGeneration.Messaging;

/// <summary>
/// Consumes <see cref="RetryQueueNames.Main"/> and guarantees no ticket PDF is ever silently
/// dropped: a successful generation acks the delivery; a non-retryable (poison) failure republishes
/// the untouched body to the dead-letter queue; a transient failure (Catalog down, Azure Blob
/// unreachable) republishes with an incremented retry-count header to the next backoff tier — capped
/// at a steady 30-minute cadence, never abandoned.
///
/// This matters more here than it looks: the buyer's confirmation email is chained behind
/// TicketPdfReady, so a message abandoned in this queue means a paying customer receives nothing at
/// all. Every republish is confirmed durably queued (publisher confirms) BEFORE the original is
/// acked, so a crash between the two self-heals via redelivery.
///
/// Structurally identical to eTicketing.Notifications' consumer of the same name — deliberately, so
/// the two read as one pattern rather than two.
/// </summary>
public sealed class RabbitMqConsumerService : BackgroundService
{
    private const string RetryCountHeader = "x-retry-count";

    private readonly RabbitMqOptions _options;
    private readonly TicketPurchasedDispatcher _dispatcher;
    private readonly ILogger<RabbitMqConsumerService> _logger;

    public RabbitMqConsumerService(
        IOptions<RabbitMqOptions> options, TicketPurchasedDispatcher dispatcher, ILogger<RabbitMqConsumerService> logger)
    {
        _options = options.Value;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // RabbitMQ may not be ready at first boot (compose's service_healthy dependency covers the
        // common case; this loop also covers a later broker restart).
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Veza sa RabbitMQ je izgubljena ili nije uspostavljena. Pokušavam ponovo za 10 sekundi.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { HostName = _options.Host };
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);

        await RabbitMqTopology.DeclareAsync(channel, ct);

        // Rendering a PDF is CPU-bound and takes real time, unlike firing an HTTP request. Without
        // a prefetch limit the broker would push the entire backlog at this one consumer at once.
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 4, global: false, ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) => await HandleDeliveryAsync(channel, delivery, ct);

        await channel.BasicConsumeAsync(RetryQueueNames.Main, autoAck: false, consumer, cancellationToken: ct);
        _logger.LogInformation("PdfGeneration consumer je spreman i sluša red '{Queue}'.", RetryQueueNames.Main);

        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        var routingKey = delivery.RoutingKey;

        try
        {
            await _dispatcher.DispatchAsync(routingKey, delivery.Body, ct);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        }
        catch (PoisonMessageException ex)
        {
            _logger.LogError(ex, "Poruka za '{RoutingKey}' se ne može obraditi — premještam u dead-letter red.", routingKey);
            await RepublishAsync(channel, RetryQueueNames.DeadLetter, delivery.Body, retryCount: null, ct);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex)
        {
            var retryCount = ReadRetryCount(delivery) + 1;
            var targetQueue = RetryQueueNames.ForAttempt(retryCount);
            _logger.LogWarning(ex,
                "Generisanje PDF-a za '{RoutingKey}' nije uspjelo (pokušaj {RetryCount}) — zakazujem ponovni pokušaj u redu '{Queue}'.",
                routingKey, retryCount, targetQueue);
            await RepublishAsync(channel, targetQueue, delivery.Body, retryCount, ct);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        }
    }

    private static async Task RepublishAsync(IChannel channel, string targetQueue, ReadOnlyMemory<byte> body, int? retryCount, CancellationToken ct)
    {
        var properties = new BasicProperties { Persistent = true };
        if (retryCount is not null)
        {
            properties.Headers = new Dictionary<string, object?> { [RetryCountHeader] = retryCount.Value };
        }

        // Default exchange ("") routes purely by routing key == queue name — no custom exchange
        // needed to reach a specific retry/dead-letter queue directly.
        await channel.BasicPublishAsync(
            exchange: string.Empty, routingKey: targetQueue, mandatory: false, basicProperties: properties, body: body, cancellationToken: ct);
    }

    private static int ReadRetryCount(BasicDeliverEventArgs delivery)
    {
        if (delivery.BasicProperties.Headers is not { } headers || !headers.TryGetValue(RetryCountHeader, out var raw) || raw is null)
            return 0;

        // RabbitMQ's AMQP field-table decoding can hand back different CLR numeric types depending
        // on how the value was wire-encoded — defensively coerce whichever one arrives.
        return raw switch
        {
            int i => i,
            long l => (int)l,
            byte[] bytes => int.Parse(System.Text.Encoding.UTF8.GetString(bytes)),
            _ => Convert.ToInt32(raw)
        };
    }
}
