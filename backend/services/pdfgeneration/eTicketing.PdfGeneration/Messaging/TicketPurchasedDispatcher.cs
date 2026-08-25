using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.PdfGeneration.Documents;

namespace eTicketing.PdfGeneration.Messaging;

/// <summary>
/// Routes a raw RabbitMQ body to PDF generation and publishes the resulting
/// <see cref="TicketPdfReady"/>. Split out from RabbitMqConsumerService so the interesting part
/// (deserialize → generate → publish) is testable without a broker, mirroring
/// eTicketing.Notifications' NotificationDispatcher.
///
/// Publishing happens here rather than in the generator so the generator stays a pure
/// render-and-upload unit with no messaging concern of its own.
/// </summary>
public sealed class TicketPurchasedDispatcher
{
    private readonly ITicketPdfGenerator _generator;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<TicketPurchasedDispatcher> _logger;

    public TicketPurchasedDispatcher(
        ITicketPdfGenerator generator, IEventPublisher eventPublisher, ILogger<TicketPurchasedDispatcher> logger)
    {
        _generator = generator;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task DispatchAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken ct = default)
    {
        if (routingKey != EventNames.TicketPurchased)
            throw new PoisonMessageException($"Nepoznat routing key: {routingKey}");

        var order = Deserialize<TicketPurchased>(body, routingKey);

        if (order.Tickets.Count == 0)
            throw new PoisonMessageException($"Narudžba {order.OrderId} nema nijednu ulaznicu.");

        _logger.LogInformation(
            "Generišem {Count} PDF ulaznica za narudžbu {OrderId}.", order.Tickets.Count, order.OrderId);

        var ready = await _generator.GenerateAsync(order, ct)
            ?? throw new PoisonMessageException($"Proizvod iz narudžbe {order.OrderId} više ne postoji.");

        // Two consumers pick this up: eTicketing.Notifications sends the confirmation email with
        // every PDF attached, and eTicketing.Ticketing flips the tickets Confirmed → Ready.
        await _eventPublisher.PublishAsync(EventNames.TicketPdfReady, ready, ct);
    }

    private static T Deserialize<T>(ReadOnlyMemory<byte> body, string routingKey)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body.Span)
                ?? throw new PoisonMessageException($"Prazan payload za event '{routingKey}'.");
        }
        catch (JsonException ex)
        {
            throw new PoisonMessageException($"Neispravan JSON za event '{routingKey}'.", ex);
        }
    }
}
