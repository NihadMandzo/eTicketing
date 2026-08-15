using eTicketing.Notifications.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace eTicketing.Notifications.Messaging;

/// <summary>Consumes <see cref="RetryQueueNames.Main"/> and guarantees no email is ever silently
/// lost: a successful send acks the delivery; a non-retryable (poison) failure republishes the
/// untouched body to the dead-letter queue; a transient failure (Brevo unreachable, after
/// BrevoEmailSender's own resilience retries are exhausted) republishes with an incremented
/// retry-count header to the next backoff tier (see RetryQueueNames.ForAttempt) — capped at a
/// steady 30-minute cadence, never abandoned. Every republish is confirmed durably queued
/// (publisher confirms) BEFORE the original delivery is acked, so a crash between the two steps
/// self-heals via redelivery instead of losing the message.</summary>
public sealed class RabbitMqConsumerService : BackgroundService
{
    private const string RetryCountHeader = "x-retry-count";

    private readonly RabbitMqOptions _options;
    private readonly NotificationDispatcher _dispatcher;
    private readonly ILogger<RabbitMqConsumerService> _logger;

    public RabbitMqConsumerService(
        IOptions<RabbitMqOptions> options, NotificationDispatcher dispatcher, ILogger<RabbitMqConsumerService> logger)
    {
        _options = options.Value;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // RabbitMQ may not be ready yet at first boot (docker-compose's service_healthy
        // dependency covers the common case, but this loop also covers a later broker restart —
        // the service keeps retrying instead of crashing out).
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
        // Publisher confirmations: BasicPublishAsync below doesn't complete until the broker has
        // durably accepted the message — this is what makes "confirm the retry/dead-letter copy
        // landed before acking the original" a real guarantee rather than a hope.
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);

        await RabbitMqTopology.DeclareAsync(channel, ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) => await HandleDeliveryAsync(channel, delivery, ct);

        await channel.BasicConsumeAsync(RetryQueueNames.Main, autoAck: false, consumer, cancellationToken: ct);
        _logger.LogInformation("Notifications consumer je spreman i sluša red '{Queue}'.", RetryQueueNames.Main);

        // Keep this connection/channel alive for as long as the host runs (or until the
        // connection itself throws, e.g. broker restart) — the outer ExecuteAsync loop handles
        // reconnection.
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
                "Slanje emaila za '{RoutingKey}' nije uspjelo (pokušaj {RetryCount}) — zakazujem ponovni pokušaj u redu '{Queue}'.",
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

        // RabbitMQ's AMQP field-table decoding can hand back different CLR numeric types
        // depending on how the value was wire-encoded — defensively coerce whichever one arrives.
        return raw switch
        {
            int i => i,
            long l => (int)l,
            byte[] bytes => int.Parse(System.Text.Encoding.UTF8.GetString(bytes)),
            _ => Convert.ToInt32(raw)
        };
    }
}
