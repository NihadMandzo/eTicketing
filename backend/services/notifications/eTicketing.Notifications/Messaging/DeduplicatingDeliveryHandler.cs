using Microsoft.Extensions.Logging;

namespace eTicketing.Notifications.Messaging;

/// <summary>
/// Sends each email once, even though its event can arrive more than once.
///
/// <para>Delivery into this service is at-least-once by design: the transactional outbox publishes a
/// row again if it died between the broker's confirm and deleting the row, and RabbitMQ redelivers
/// anything unacked when a consumer's connection drops. Every such repeat used to send the buyer
/// another copy of the same email.</para>
///
/// <para><b>Marked after the send, never before.</b> Marking first would turn a send that fails —
/// Brevo down — into a message the retry ladder then skips as "already processed", losing the email
/// outright. The price of this order is a narrow window: a process that dies between the send and
/// the mark sends again on redelivery. A duplicate there is the safer failure.</para>
///
/// <para><b>Fails open.</b> If the store itself is unreachable the email is sent anyway and the
/// failure logged. Dedupe exists to stop an occasional duplicate; letting a Redis outage block every
/// verification code and purchase confirmation would trade a rare nuisance for a real outage.</para>
///
/// <para>Two replicas handed the same message at the same instant can both pass the check. That is
/// not the case this guards against — RabbitMQ does not deliver one message to two consumers at
/// once — so it is left unguarded rather than paid for with a lock.</para>
/// </summary>
public sealed class DeduplicatingDeliveryHandler
{
    private readonly NotificationDispatcher _dispatcher;
    private readonly IProcessedMessageStore _store;
    private readonly ILogger<DeduplicatingDeliveryHandler> _logger;

    public DeduplicatingDeliveryHandler(
        NotificationDispatcher dispatcher,
        IProcessedMessageStore store,
        ILogger<DeduplicatingDeliveryHandler> logger)
    {
        _dispatcher = dispatcher;
        _store = store;
        _logger = logger;
    }

    /// <summary>False when the delivery was recognised as a repeat and skipped. Exceptions from the
    /// dispatch itself propagate unchanged, so RabbitMqConsumerService's poison/retry routing sees
    /// exactly what it saw before.</summary>
    public async Task<bool> HandleAsync(
        string routingKey, string? messageId, ReadOnlyMemory<byte> body, CancellationToken ct = default)
    {
        var key = DeliveryDedupeKey.Resolve(routingKey, messageId, body);

        if (key is not null && await IsProcessedAsync(key, ct))
        {
            _logger.LogInformation(
                "Poruka '{RoutingKey}' ({DedupeKey}) je već obrađena — preskačem ponovno slanje.",
                routingKey, key);
            return false;
        }

        await _dispatcher.DispatchAsync(routingKey, body, ct);

        if (key is not null)
            await MarkProcessedAsync(key, ct);

        return true;
    }

    private async Task<bool> IsProcessedAsync(string key, CancellationToken ct)
    {
        try
        {
            return await _store.IsProcessedAsync(key, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Provjera duplikata za {DedupeKey} nije uspjela — šaljem email bez provjere.", key);
            return false;
        }
    }

    private async Task MarkProcessedAsync(string key, CancellationToken ct)
    {
        try
        {
            await _store.MarkProcessedAsync(key, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Swallowed on purpose. The email has already gone out; rethrowing would send the
            // delivery down the retry ladder and send it a second time — the very thing this class
            // exists to prevent.
            _logger.LogWarning(ex,
                "Email je poslan, ali {DedupeKey} nije zabilježen kao obrađen — ponovna isporuka bi ga poslala opet.",
                key);
        }
    }
}
