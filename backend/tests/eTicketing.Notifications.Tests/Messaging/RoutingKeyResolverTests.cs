using eTicketing.Contracts.Events;
using eTicketing.Notifications.Messaging;
using FluentAssertions;

namespace eTicketing.Notifications.Tests.Messaging;

/// <summary>Guards the regression that silently dead-lettered every retried email: a message
/// republished to a retry tier comes back to the main queue under the queue name, so without the
/// stashed original key the dispatcher rejected it as "Nepoznat routing key:
/// notifications.email" and the confirmation email was never sent.</summary>
public class RoutingKeyResolverTests
{
    [Fact]
    public void Resolve_ForFirstDeliveryFromTheExchange_UsesTheDeliveryRoutingKey()
    {
        // A message straight off the topic exchange has no Type property set.
        var routingKey = RoutingKeyResolver.Resolve(originalRoutingKey: null, deliveryRoutingKey: EventNames.TicketPdfReady);

        routingKey.Should().Be(EventNames.TicketPdfReady);
    }

    [Fact]
    public void Resolve_ForRetriedDeliveryDeadLetteredBackToMain_RecoversTheOriginalEventKey()
    {
        // The retry queue's x-dead-letter-routing-key rewrites RoutingKey to the main queue name.
        var routingKey = RoutingKeyResolver.Resolve(
            originalRoutingKey: EventNames.TicketPdfReady, deliveryRoutingKey: RetryQueueNames.Main);

        routingKey.Should().Be(EventNames.TicketPdfReady);
    }

    [Fact]
    public void Resolve_ForRetriedDeliveryStillSittingOnARetryTier_RecoversTheOriginalEventKey()
    {
        var routingKey = RoutingKeyResolver.Resolve(
            originalRoutingKey: EventNames.TicketPdfReady, deliveryRoutingKey: RetryQueueNames.ForAttempt(1));

        routingKey.Should().Be(EventNames.TicketPdfReady);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ForBlankStashedKey_FallsBackToTheDeliveryRoutingKey(string stashed)
    {
        var routingKey = RoutingKeyResolver.Resolve(stashed, EventNames.TicketPdfReady);

        routingKey.Should().Be(EventNames.TicketPdfReady);
    }

    [Fact]
    public void Resolve_NeverReturnsAQueueNameWhenAnOriginalKeyWasStashed()
    {
        foreach (var (queueName, _) in RetryQueueNames.Tiers)
        {
            RoutingKeyResolver.Resolve(EventNames.TicketPdfReady, queueName)
                .Should().Be(EventNames.TicketPdfReady);
        }
    }
}
