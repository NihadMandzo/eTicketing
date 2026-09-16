using System.Text;
using System.Text.Json;
using eTicketing.Contracts.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace eTicketing.Shared.Messaging;

/// <summary>
/// The one RabbitMQ publisher, replacing five near-identical copies.
///
/// Three things changed in the consolidation, all of which the copies got wrong in the same way:
///
/// <list type="bullet">
/// <item><b>Messages are persistent.</b> The copies published with default properties, which means
/// <c>delivery_mode 1</c> — held in memory only. A broker restart dropped every unconsumed message
/// regardless of the queues being declared durable, which is the half of durability that is easy to
/// miss because the queues *look* right in the management UI.</item>
/// <item><b>Publishes wait for the broker to confirm.</b> Without confirms, <c>BasicPublishAsync</c>
/// returns as soon as the bytes are written to the socket, so an unroutable or rejected message
/// looks identical to a delivered one. With them, a failure throws — which is what lets the outbox
/// keep the row and try again rather than deleting it on a false success.</item>
/// <item><b>The connection is cached.</b> Every copy opened a TCP connection, a channel and an
/// exchange declaration per message, then tore it all down. That is several round trips of latency
/// on the purchase critical path for what should be one.</item>
/// </list>
///
/// <para><b>This one does not swallow exceptions.</b> The copies each caught everything and logged,
/// because they published after the data was already committed and had nothing useful to do with a
/// failure. Its callers now do: <see cref="OutboxEventPublisher{TContext}"/> never reaches the
/// broker at all on the write path, and the dispatcher that does keeps the row and retries. A
/// caller that genuinely wants fire-and-forget has to say so.</para>
/// </summary>
public sealed class RabbitMqEventPublisher : IEventPublisher, IRawEventPublisher, IAsyncDisposable
{
    private readonly RabbitMqPublisherOptions _options;
    private readonly ILogger<RabbitMqEventPublisher> _logger;

    // One connection for the process. Guarded rather than created eagerly in the constructor: this
    // is a singleton resolved during startup, and the broker is frequently not accepting
    // connections yet at that point.
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private IConnection? _connection;

    public RabbitMqEventPublisher(
        IOptions<RabbitMqPublisherOptions> options, ILogger<RabbitMqEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        // A channel per publish, over a shared connection. Channels are cheap; sharing one across
        // threads is not safe, and this is called concurrently from request handlers.
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);

        await channel.ExchangeDeclareAsync(
            EventNames.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        await channel.BasicPublishAsync(
            exchange: EventNames.Exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: new BasicProperties
            {
                // Survive a broker restart. See the class comment — durable queues alone do not.
                Persistent = true,
                MessageId = Guid.NewGuid().ToString("N"),
                ContentType = "application/json",
            },
            body: body,
            cancellationToken: ct);
    }

    /// <inheritdoc />
    public async Task PublishRawAsync(
        string routingKey, string payloadJson, Guid messageId, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);

        await channel.ExchangeDeclareAsync(
            EventNames.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

        await channel.BasicPublishAsync(
            exchange: EventNames.Exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: new BasicProperties
            {
                Persistent = true,
                MessageId = messageId.ToString("N"),
                ContentType = "application/json",
            },
            body: Encoding.UTF8.GetBytes(payloadJson),
            cancellationToken: ct);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _connectionGate.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            // A connection that exists but has closed — broker restart, network blip — is disposed
            // rather than reused; the client does not resurrect one.
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            _connection = await CreateFactory().CreateConnectionAsync(ct);
            _logger.LogInformation("Uspostavljena veza prema RabbitMQ ({Host}).", _options.Host);
            return _connection;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    /// <summary>Host may be a bare name or a full amqp URI — both appear in this repo's
    /// configuration (compose passes a URI, appsettings a host name), and guessing wrong is a
    /// connection failure at startup rather than a compile error.</summary>
    private ConnectionFactory CreateFactory() =>
        Uri.TryCreate(_options.Host, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("amqp")
            ? new ConnectionFactory { Uri = uri }
            : new ConnectionFactory { HostName = _options.Host };

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();

        _connectionGate.Dispose();
    }
}
