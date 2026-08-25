using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.Ticketing.Business.Integration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace eTicketing.Ticketing.Api.Infrastructure.Messaging;

/// <summary>
/// Ticketing's only inbound queue. Two routing keys land here:
///
///  - <c>ticket-pdf.ready</c> from eTicketing.PdfGeneration — flip Confirmed → Ready.
///  - <c>product.updated</c> from eTicketing.Catalog — fan out per-buyer change notifications.
///
/// Modelled on eTicketing.Notifications' RabbitMqConsumerService (reconnect loop, manual ack,
/// publisher confirms before acking a republish) — and, like eTicketing.PdfGeneration's, it caps
/// prefetch rather than letting the broker push a whole backlog at once. But it takes a
/// deliberately blunter failure policy than either:
/// that service's five-tier backoff ladder exists because it's retrying a third-party HTTP API that
/// can be down for hours. Everything here is a local database write, so a failure is either
/// transient-and-brief (requeue once) or a real bug (dead-letter it and move on) — a 30-minute
/// steady-state retry tier would just delay noticing the bug.
///
/// This is an ASP.NET Core app, so the handlers are scoped services: every message opens its own DI
/// scope rather than capturing a DbContext for the process lifetime.
/// </summary>
public sealed class TicketingRabbitMqConsumerService : BackgroundService
{
    private const string QueueName = "ticketing.inbound";
    private const string DeadLetterQueueName = "ticketing.inbound.deadletter";
    private const string RedeliveredHeader = "x-ticketing-redelivered";

    private static readonly string[] RoutingKeys = [EventNames.TicketPdfReady, EventNames.ProductUpdated];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TicketingRabbitMqConsumerService> _logger;

    public TicketingRabbitMqConsumerService(
        IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<TicketingRabbitMqConsumerService> logger)
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
        var factory = new ConnectionFactory { HostName = _configuration["RabbitMq:Host"] ?? "localhost" };
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);

        // Every message here opens a DI scope and does a database write. Without a prefetch limit
        // the broker would hand this single consumer an entire backlog at once — after a restart or
        // an outage that's a burst of concurrent DbContexts rather than a steady drain.
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
        _logger.LogInformation("Ticketing consumer je spreman i sluša red '{Queue}'.", QueueName);

        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        // A retry is republished through the DEFAULT exchange (routing key == queue name), which
        // overwrites delivery.RoutingKey with "ticketing.inbound" and would otherwise make the
        // second attempt land in the unknown-key branch instead of re-running the real handler.
        // RepublishAsync stashes the original key in BasicProperties.Type for exactly this.
        var routingKey = delivery.BasicProperties.Type ?? delivery.RoutingKey;

        try
        {
            await DispatchAsync(routingKey, delivery.Body, ct);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex)
        {
            // One retry, then the dead-letter queue. A DB write that fails twice in a row is a bug
            // or an outage that needs a human, not something a longer backoff fixes — and unlike an
            // email, nothing here is lost by parking it: the tickets and their PDFs still exist,
            // the message can be replayed from the DLQ once the cause is fixed.
            var alreadyRetried = delivery.BasicProperties.Headers?.ContainsKey(RedeliveredHeader) == true;
            var targetQueue = alreadyRetried ? DeadLetterQueueName : QueueName;

            _logger.LogError(ex,
                "Obrada poruke '{RoutingKey}' nije uspjela — premještam u red '{Queue}'.", routingKey, targetQueue);

            await RepublishAsync(channel, targetQueue, routingKey, delivery.Body, ct);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        }
    }

    private async Task DispatchAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        switch (routingKey)
        {
            case EventNames.TicketPdfReady:
                var pdfReady = Deserialize<TicketPdfReady>(body, routingKey);
                await scope.ServiceProvider.GetRequiredService<ITicketPdfCompletionService>().ApplyAsync(pdfReady, ct);
                break;

            case EventNames.ProductUpdated:
                var productUpdated = Deserialize<ProductUpdated>(body, routingKey);
                await scope.ServiceProvider.GetRequiredService<IProductChangeNotifier>().NotifyBuyersAsync(productUpdated, ct);
                break;

            default:
                // Only reachable if a binding is added above without a matching case — that's a
                // deployment mistake worth seeing loudly, and the catch above parks the message.
                throw new InvalidOperationException($"Nepoznat routing key: {routingKey}");
        }
    }

    private static async Task RepublishAsync(
        IChannel channel, string targetQueue, string routingKey, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            // Preserved so the DLQ copy still says which event it was — the default exchange routes
            // by queue name, which would otherwise erase it.
            Type = routingKey,
            Headers = new Dictionary<string, object?> { [RedeliveredHeader] = 1 },
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty, routingKey: targetQueue, mandatory: false, basicProperties: properties, body: body, cancellationToken: ct);
    }

    private static T Deserialize<T>(ReadOnlyMemory<byte> body, string routingKey)
        => JsonSerializer.Deserialize<T>(body.Span)
           ?? throw new InvalidOperationException($"Prazan payload za event '{routingKey}'.");
}
