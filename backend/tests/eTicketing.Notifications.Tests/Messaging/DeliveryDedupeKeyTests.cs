using System.Text;
using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.Notifications.Messaging;
using FluentAssertions;

namespace eTicketing.Notifications.Tests.Messaging;

/// <summary>What counts as "the same email". Getting this wrong fails in one of two quiet ways: a key
/// too narrow lets a repeat through (the duplicate this exists to stop), and a key too broad
/// swallows a genuinely different email as a repeat — which is worse, because nobody receives it.</summary>
public class DeliveryDedupeKeyTests
{
    private static ReadOnlyMemory<byte> TicketPdfReadyBody(Guid orderId) => JsonSerializer.SerializeToUtf8Bytes(
        new TicketPdfReady(
            orderId, Guid.NewGuid(), Guid.NewGuid(), "buyer@example.com", "Koncert", null, "Sarajevo", 50m,
            [new TicketPdf(Guid.NewGuid(), [1, 2, 3], "ulaznica.pdf", "VIP", null, 50m)]));

    [Fact]
    public void Resolve_WithAMessageId_KeysOnIt()
    {
        var key = DeliveryDedupeKey.Resolve(EventNames.VerificationEmailRequested, "abc123", "{}"u8.ToArray());

        key.Should().Be("message:abc123");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WithoutAMessageId_ReturnsNullSoTheMessageIsProcessedAsBefore(string? messageId)
    {
        var key = DeliveryDedupeKey.Resolve(EventNames.VerificationEmailRequested, messageId, "{}"u8.ToArray());

        key.Should().BeNull();
    }

    [Fact]
    public void Resolve_ForTwoDifferentMessagesWithDifferentIds_ProducesDifferentKeys()
    {
        var first = DeliveryDedupeKey.Resolve(EventNames.PasswordResetRequested, "id-1", "{}"u8.ToArray());
        var second = DeliveryDedupeKey.Resolve(EventNames.PasswordResetRequested, "id-2", "{}"u8.ToArray());

        first.Should().NotBe(second);
    }

    [Fact]
    public void Resolve_ForTicketPdfReady_KeysOnTheOrderRatherThanTheMessageId()
    {
        // PdfGeneration mints a fresh id every time it republishes, so a redelivered
        // ticket.purchased arrives here as a second ticket-pdf.ready under a new id. Only the order
        // identifies it as the same confirmation email.
        var orderId = Guid.NewGuid();

        var first = DeliveryDedupeKey.Resolve(EventNames.TicketPdfReady, "first-id", TicketPdfReadyBody(orderId));
        var repeat = DeliveryDedupeKey.Resolve(EventNames.TicketPdfReady, "second-id", TicketPdfReadyBody(orderId));

        first.Should().Be($"{EventNames.TicketPdfReady}:order:{orderId:N}");
        repeat.Should().Be(first);
    }

    [Fact]
    public void Resolve_ForTicketPdfReadyOfTwoDifferentOrders_ProducesDifferentKeys()
    {
        // Two purchases by the same buyer — or two renewals of the same subscription, each of which
        // gets its own order — are two emails, not one.
        var first = DeliveryDedupeKey.Resolve(EventNames.TicketPdfReady, "same-id", TicketPdfReadyBody(Guid.NewGuid()));
        var second = DeliveryDedupeKey.Resolve(EventNames.TicketPdfReady, "same-id", TicketPdfReadyBody(Guid.NewGuid()));

        first.Should().NotBe(second);
    }

    [Fact]
    public void Resolve_ForTicketPdfReadyWithAnUnreadableBody_FallsBackToTheMessageId()
    {
        // Malformed JSON is the dispatcher's to dead-letter as poison; the key must not throw first.
        var key = DeliveryDedupeKey.Resolve(EventNames.TicketPdfReady, "abc123", Encoding.UTF8.GetBytes("{nije json"));

        key.Should().Be("message:abc123");
    }

    [Fact]
    public void Resolve_ForAnotherEventThatHappensToCarryAnOrderIdField_StillKeysOnTheMessageId()
    {
        var body = Encoding.UTF8.GetBytes($$"""{"OrderId":"{{Guid.NewGuid()}}"}""");

        var key = DeliveryDedupeKey.Resolve(EventNames.PaymentFailed, "abc123", body);

        key.Should().Be("message:abc123");
    }
}
