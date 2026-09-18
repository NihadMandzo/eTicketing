using Microsoft.Extensions.Logging;

namespace eTicketing.Notifications.Messaging;

/// <summary>
/// Sends each email once, however many times its event arrives.
///
/// <para>Delivery into this service is at-least-once by design: the transactional outbox publishes a
/// row again if it died between the broker's confirm and deleting the row, and RabbitMQ redelivers
/// anything unacked when a consumer's connection drops. Every such repeat used to send the buyer
/// another copy of the same email.</para>
///
/// <para><b>Claim, send, mark — in that order.</b> Claiming is one atomic operation
/// (<see cref="IProcessedMessageStore.TryClaimAsync"/>), which is what makes this hold with more than
/// one replica: a check followed by a mark would let two consumers both read "not processed" and
/// both send. Marking happens only after the send returns, because marking first would turn a send
/// that fails — Brevo down — into a message the retry ladder then skips as already processed, losing
/// the email outright. A failed send releases the claim so the retry does not have to wait the lease
/// out.</para>
///
/// <para><b>The remaining window is narrow and deliberate.</b> A process that dies between the send
/// and the mark leaves its claim to expire, and the redelivery sends again. A duplicate there is the
/// safer failure.</para>
///
/// <para><b>Fails open.</b> If the store itself is unreachable the email is sent anyway and the
/// failure logged. Dedupe exists to stop an occasional duplicate; letting a Redis outage block every
/// verification code and purchase confirmation would trade a rare nuisance for a real outage.</para>
/// </summary>
public sealed class DeduplicatingDeliveryHandler
{
    /// <summary>How long a claim survives a consumer that never comes back. Comfortably longer than a
    /// send can take — BrevoEmailSender's pipeline is a 10-second timeout and three retries — and
    /// short enough that a crashed replica does not hold an email up for long.</summary>
    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

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
    /// exactly what it saw before; a delivery another consumer is working on right now raises
    /// <see cref="DeliveryInProgressException"/>, which takes the same retry ladder.</summary>
    public async Task<bool> HandleAsync(
        string routingKey, string? messageId, ReadOnlyMemory<byte> body, CancellationToken ct = default)
    {
        var key = DeliveryDedupeKey.Resolve(routingKey, messageId, body);

        if (key is null)
        {
            await _dispatcher.DispatchAsync(routingKey, body, ct);
            return true;
        }

        switch (await TryClaimAsync(key, ct))
        {
            case DeliveryClaim.AlreadyProcessed:
                _logger.LogInformation(
                    "Poruka '{RoutingKey}' ({DedupeKey}) je već obrađena — preskačem ponovno slanje.",
                    routingKey, key);
                return false;

            case DeliveryClaim.InProgress:
                throw new DeliveryInProgressException(
                    $"Poruku '{routingKey}' ({key}) trenutno obrađuje drugi consumer — pokušavam ponovo kasnije.");
        }

        try
        {
            await _dispatcher.DispatchAsync(routingKey, body, ct);
        }
        catch
        {
            await ReleaseClaimAsync(key);
            throw;
        }

        await MarkProcessedAsync(key, ct);
        return true;
    }

    private async Task<DeliveryClaim> TryClaimAsync(string key, CancellationToken ct)
    {
        try
        {
            return await _store.TryClaimAsync(key, ClaimLease, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Provjera duplikata za {DedupeKey} nije uspjela — šaljem email bez provjere.", key);
            return DeliveryClaim.Claimed;
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

    /// <summary>Best effort, and deliberately not cancellable: the send has already failed, and the
    /// delivery is on its way to the retry ladder. A claim that cannot be released simply expires.</summary>
    private async Task ReleaseClaimAsync(string key)
    {
        try
        {
            await _store.ReleaseClaimAsync(key, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Oslobađanje rezervacije za {DedupeKey} nije uspjelo — istječe sama nakon {Lease}.",
                key, ClaimLease);
        }
    }
}
