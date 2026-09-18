using System.Text.Json;
using eTicketing.Catalog.Business.Recommendations;
using eTicketing.Contracts.Events;
using eTicketing.Shared.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace eTicketing.Catalog.Api.Infrastructure.Messaging;

/// <summary>
/// Catalog's first inbound queue — until recommendations existed this service only ever published.
/// One routing key lands here: <c>ticket.purchased</c> from eTicketing.Ticketing, recorded as the
/// strongest signal the recommender has.
///
/// Modelled on eTicketing.Ticketing.Api's TicketingRabbitMqConsumerService (reconnect loop, manual
/// ack, capped prefetch, one DI scope per message, one retry then dead-letter) rather than on
/// eTicketing.Notifications' five-tier backoff ladder. That ladder exists because Notifications is
/// retrying a third-party email API that can be down for hours and a lost message means a buyer
/// never gets their ticket. Nothing of the sort is at stake here: this is a local database write,
/// and a dropped signal costs one row of training data out of thousands — the recommendation is
/// marginally worse, nobody is missing a ticket. Parking a repeatedly failing message where a human
/// can see it beats retrying it every 30 minutes for a day.
///
/// Every message goes through the transactional inbox, keyed on this queue's name, so a redelivered
/// purchase is recorded once rather than counted again.
/// </summary>
public sealed class CatalogRabbitMqConsumerService : BackgroundService
{
    /// <summary>Also the consumer name the inbox records processed messages under.</summary>
    public const string QueueName = "catalog.recommendations";
    private const string DeadLetterQueueName = "catalog.recommendations.deadletter";
    private const string RedeliveredHeader = "x-catalog-redelivered";

    private static readonly string[] RoutingKeys = [EventNames.TicketPurchased];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CatalogRabbitMqConsumerService> _logger;

    public CatalogRabbitMqConsumerService(
        IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<CatalogRabbitMqConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
        // RabbitMq:Host doubles as either a bare hostname (local docker-compose broker) or a full
        // amqp(s)://user:pass@host/vhost connection string (self-hosted RabbitMQ in Azure needs
        // real credentials — the default guest user only accepts localhost connections) — HostName
        // alone can't parse the latter, it would just try to DNS-resolve the whole URI string.
        var host = _configuration["RabbitMq:Host"] ?? "localhost";
        var factory = Uri.TryCreate(host, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("amqp")
            ? new ConnectionFactory { Uri = uri }
            : new ConnectionFactory { HostName = host };
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);

        // Each message opens a DI scope and writes to the database; without this the broker would
        // hand a restarted consumer its whole backlog at once.
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, ct);

        await channel.ExchangeDeclareAsync(EventNames.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);

        foreach (var routingKey in RoutingKeys)
        {
            await channel.QueueBindAsync(QueueName, EventNames.Exchange, routingKey, cancellationToken: ct);
        }

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) => await HandleDeliveryAsync(channel, delivery, ct);

        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, cancellationToken: ct);
        _logger.LogInformation("Catalog consumer je spreman i sluša red '{Queue}'.", QueueName);

        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        // A retry is republished through the default exchange (routing key == queue name), which
        // overwrites delivery.RoutingKey — RepublishAsync stashes the original in Type so the
        // second attempt still reaches the right handler.
        var routingKey = delivery.BasicProperties.Type ?? delivery.RoutingKey;
        var messageId = delivery.BasicProperties.MessageId;

        try
        {
            await DispatchAsync(routingKey, messageId, delivery.Body, ct);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex)
        {
            var alreadyRetried = delivery.BasicProperties.Headers?.ContainsKey(RedeliveredHeader) == true;
            var targetQueue = alreadyRetried ? DeadLetterQueueName : QueueName;

            _logger.LogError(ex,
                "Obrada poruke '{RoutingKey}' nije uspjela — premještam u red '{Queue}'.", routingKey, targetQueue);

            await RepublishAsync(channel, targetQueue, routingKey, messageId, delivery.Body, ct);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        }
    }

    private async Task DispatchAsync(string routingKey, string? messageId, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;

        // Through the inbox, so the interaction bump and the record that it happened commit
        // together: a redelivered purchase is recognised and skipped instead of counted twice.
        var processed = await services.GetRequiredService<IInbox>().ProcessOnceAsync(
            messageId,
            QueueName,
            handlerCt => HandleAsync(services, routingKey, body, handlerCt),
            ct);

        if (!processed)
        {
            _logger.LogInformation(
                "Poruka '{RoutingKey}' ({MessageId}) je već obrađena — preskačem ponovnu obradu.", routingKey, messageId);
        }
    }

    private static async Task HandleAsync(
        IServiceProvider services, string routingKey, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        switch (routingKey)
        {
            case EventNames.TicketPurchased:
                var purchased = Deserialize<TicketPurchased>(body, routingKey);
                await services.GetRequiredService<PurchaseInteractionRecorder>().RecordAsync(purchased, ct);
                break;

            default:
                // Only reachable if a binding is added above without a matching case — a deployment
                // mistake worth seeing loudly; the catch above parks the message.
                throw new InvalidOperationException($"Nepoznat routing key: {routingKey}");
        }
    }

    private static async Task RepublishAsync(
        IChannel channel, string targetQueue, string routingKey, string? messageId, ReadOnlyMemory<byte> body,
        CancellationToken ct)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            Type = routingKey,
            // Kept so the retry is still recognisably the same message to the inbox.
            MessageId = messageId,
            Headers = new Dictionary<string, object?> { [RedeliveredHeader] = 1 },
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty, routingKey: targetQueue, mandatory: false, basicProperties: properties, body: body, cancellationToken: ct);
    }

    private static T Deserialize<T>(ReadOnlyMemory<byte> body, string routingKey)
        => JsonSerializer.Deserialize<T>(body.Span)
           ?? throw new InvalidOperationException($"Prazan payload za event '{routingKey}'.");
}
