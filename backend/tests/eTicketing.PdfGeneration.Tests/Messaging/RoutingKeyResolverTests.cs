using eTicketing.Contracts.Events;
using eTicketing.PdfGeneration.Messaging;
using FluentAssertions;

namespace eTicketing.PdfGeneration.Tests.Messaging;

/// <summary>Guards the regression that silently dead-lettered every retried ticket PDF: a message
/// republished to a retry tier comes back to the main queue under the queue name, so without the
/// stashed original key the dispatcher rejected it as "Nepoznat routing key:
/// pdfgeneration.tickets" and the buyer never received their ticket or confirmation email.</summary>
public class RoutingKeyResolverTests
{
    [Fact]
    public void Resolve_ForFirstDeliveryFromTheExchange_UsesTheDeliveryRoutingKey()
    {
        // A message straight off the topic exchange has no Type property set.
        var routingKey = RoutingKeyResolver.Resolve(originalRoutingKey: null, deliveryRoutingKey: EventNames.TicketPurchased);

        routingKey.Should().Be(EventNames.TicketPurchased);
    }

    [Fact]
    public void Resolve_ForRetriedDeliveryDeadLetteredBackToMain_RecoversTheOriginalEventKey()
    {
        // The retry queue's x-dead-letter-routing-key rewrites RoutingKey to the main queue name.
        var routingKey = RoutingKeyResolver.Resolve(
            originalRoutingKey: EventNames.TicketPurchased, deliveryRoutingKey: RetryQueueNames.Main);

        routingKey.Should().Be(EventNames.TicketPurchased);
    }

    [Fact]
    public void Resolve_ForRetriedDeliveryStillSittingOnARetryTier_RecoversTheOriginalEventKey()
    {
        var routingKey = RoutingKeyResolver.Resolve(
            originalRoutingKey: EventNames.TicketPurchased, deliveryRoutingKey: RetryQueueNames.ForAttempt(1));

        routingKey.Should().Be(EventNames.TicketPurchased);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ForBlankStashedKey_FallsBackToTheDeliveryRoutingKey(string stashed)
    {
        var routingKey = RoutingKeyResolver.Resolve(stashed, EventNames.TicketPurchased);

        routingKey.Should().Be(EventNames.TicketPurchased);
    }

    [Fact]
    public void Resolve_NeverReturnsAQueueNameWhenAnOriginalKeyWasStashed()
    {
        foreach (var (queueName, _) in RetryQueueNames.Tiers)
        {
            RoutingKeyResolver.Resolve(EventNames.TicketPurchased, queueName)
                .Should().Be(EventNames.TicketPurchased);
        }
    }
}
