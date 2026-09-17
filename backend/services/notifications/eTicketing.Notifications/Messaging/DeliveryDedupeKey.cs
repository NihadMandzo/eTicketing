using System.Text.Json;
using eTicketing.Contracts.Events;

namespace eTicketing.Notifications.Messaging;

/// <summary>
/// What makes two deliveries "the same email", for <see cref="IProcessedMessageStore"/>.
///
/// <para><b>Normally the AMQP <c>MessageId</c>.</b> The outbox publishes a row under the row's own id
/// every time, so a row published twice — the dispatcher died between the broker's confirm and the
/// delete — arrives twice with the same id.</para>
///
/// <para><b>Except <c>ticket-pdf.ready</c>, which is keyed by its order.</b> That event is not
/// published from an outbox: eTicketing.PdfGeneration mints it while consuming ticket.purchased, and
/// gives it a fresh id each time. A redelivered ticket.purchased therefore produces a second
/// ticket-pdf.ready that no id would recognise, and the buyer would get the confirmation with its
/// PDFs twice. The order is what the email is about, and it is unique per purchase and per
/// subscription renewal alike, so it is the key that actually identifies the email.</para>
///
/// <para>Null — no deduplication — when neither is available. Such a message is processed exactly as
/// it was before any of this existed.</para>
/// </summary>
public static class DeliveryDedupeKey
{
    public static string? Resolve(string routingKey, string? messageId, ReadOnlyMemory<byte> body)
    {
        if (routingKey == EventNames.TicketPdfReady && TryReadOrderId(body) is { } orderId)
            return $"{EventNames.TicketPdfReady}:order:{orderId:N}";

        return string.IsNullOrWhiteSpace(messageId) ? null : $"message:{messageId}";
    }

    /// <summary>Reads only the one field, tolerating anything else about the body. A malformed body
    /// is NotificationDispatcher's to reject as poison, not this method's.</summary>
    private static Guid? TryReadOrderId(ReadOnlyMemory<byte> body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.Equals(nameof(TicketPdfReady.OrderId), StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String
                    && property.Value.TryGetGuid(out var orderId))
                {
                    return orderId;
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
