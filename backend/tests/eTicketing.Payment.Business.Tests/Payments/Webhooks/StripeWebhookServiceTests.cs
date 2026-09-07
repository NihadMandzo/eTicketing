using eTicketing.Contracts.Events;
using eTicketing.Payment.Business.Tests.TestFixtures;
using FluentAssertions;
using Moq;
using Stripe;

namespace eTicketing.Payment.Business.Tests.Payments.Webhooks;

/// <summary>
/// Signature verification is behind IStripeSignatureVerifier, so these tests hand the service
/// already-parsed Stripe events instead of computing real HMACs. That keeps the focus on the parts
/// that can actually go wrong in this codebase -- de-duplication, and which events are allowed to
/// mint a renewal -- and keeps the suite off the network. The real ConstructEvent call is covered by
/// the manual test plan instead.
/// </summary>
public class StripeWebhookServiceTests : IDisposable
{
    private readonly PaymentTestContext _fixture = new();

    private const string Signature = "t=1,v1=whatever";

    private void Verifies(Event stripeEvent) =>
        _fixture.SignatureVerifier
            .Setup(v => v.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(stripeEvent);

    private static Event InvoiceEvent(string type, string subscriptionId, string billingReason, long amountPaid = 6000) =>
        new()
        {
            Id = $"evt_{Guid.NewGuid():N}",
            Type = type,
            Data = new EventData
            {
                Object = new Invoice
                {
                    BillingReason = billingReason,
                    AmountPaid = amountPaid,
                    Currency = "eur",
                    PeriodStart = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                    // Stripe's period end is the next billing instant, i.e. exclusive.
                    PeriodEnd = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                    Parent = new InvoiceParent
                    {
                        SubscriptionDetails = new InvoiceParentSubscriptionDetails { SubscriptionId = subscriptionId },
                    },
                },
            },
        };

    [Fact]
    public async Task HandleAsync_WithAnInvalidSignature_IsRejectedAndNothingIsPublished()
    {
        _fixture.SignatureVerifier
            .Setup(v => v.Verify(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((Event?)null);

        var result = await _fixture.CreateWebhookService().HandleAsync("{}", Signature);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("payment.invalid_signature");
        // Unauthorized maps to a 4xx, which is the ONE response Stripe must not retry.
        result.Error.Type.Should().Be(eTicketing.Contracts.Results.ErrorType.Unauthorized);
        _fixture.EventPublisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_ForARenewalInvoice_PublishesSubscriptionRenewedWithAnInclusivePeriodEnd()
    {
        Verifies(InvoiceEvent("invoice.paid", "sub_1", "subscription_cycle"));

        var result = await _fixture.CreateWebhookService().HandleAsync("{}", Signature);

        result.IsSuccess.Should().BeTrue();
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(
                EventNames.SubscriptionRenewed,
                It.Is<SubscriptionRenewed>(m =>
                    m.ProviderSubscriptionId == "sub_1"
                    && m.AmountPaid == 60m
                    && m.PeriodStart == new DateOnly(2026, 10, 1)
                    // Inclusive last day, matching Ticket.ValidTo -- one day back from Stripe's
                    // exclusive period end.
                    && m.PeriodEnd == new DateOnly(2026, 10, 31)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The race this guards against: period one is minted synchronously by POST /purchases so the
    /// buyer leaves checkout holding a ticket, and Stripe ALSO sends invoice.paid for it with
    /// billing_reason "subscription_create". Acting on that would mint a duplicate.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ForTheFirstInvoiceOfASubscription_PublishesNothing()
    {
        Verifies(InvoiceEvent("invoice.paid", "sub_1", "subscription_create"));

        var result = await _fixture.CreateWebhookService().HandleAsync("{}", Signature);

        result.IsSuccess.Should().BeTrue();
        _fixture.EventPublisher.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Stripe delivers at-least-once and retries anything that is not a 2xx, so the same renewal
    /// genuinely does arrive twice. Without the StripeEvent table that would mint a second parking
    /// ticket for a month the buyer only paid for once.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithTheSameEventDeliveredTwice_ActsOnlyOnce()
    {
        var delivered = InvoiceEvent("invoice.paid", "sub_1", "subscription_cycle");
        Verifies(delivered);

        var first = await _fixture.CreateWebhookService().HandleAsync("{}", Signature);
        var second = await _fixture.CreateWebhookService().HandleAsync("{}", Signature);

        // Both answer 2xx -- a redelivery is not an error, it is expected.
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();

        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(EventNames.SubscriptionRenewed, It.IsAny<SubscriptionRenewed>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _fixture.DbContext.StripeEvents.Count(e => e.Id == delivered.Id).Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_ForAFailedRenewalInvoice_PublishesSubscriptionPaymentFailed()
    {
        Verifies(InvoiceEvent("invoice.payment_failed", "sub_1", "subscription_cycle"));

        var result = await _fixture.CreateWebhookService().HandleAsync("{}", Signature);

        result.IsSuccess.Should().BeTrue();
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(
                EventNames.SubscriptionPaymentFailed,
                It.Is<SubscriptionPaymentFailed>(m => m.ProviderSubscriptionId == "sub_1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ForADeletedSubscription_PublishesSubscriptionCancelled()
    {
        Verifies(new Event
        {
            Id = $"evt_{Guid.NewGuid():N}",
            Type = "customer.subscription.deleted",
            Data = new EventData { Object = new Subscription { Id = "sub_1", Status = "canceled" } },
        });

        var result = await _fixture.CreateWebhookService().HandleAsync("{}", Signature);

        result.IsSuccess.Should().BeTrue();
        _fixture.EventPublisher.Verify(
            p => p.PublishAsync(
                EventNames.SubscriptionCancelled,
                It.Is<SubscriptionCancelled>(m => m.ProviderSubscriptionId == "sub_1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Deliberately a success. Stripe sends whatever the account is subscribed to, and answering
    /// non-2xx to an event we have no opinion about would make it redeliver on a backoff schedule
    /// indefinitely.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ForAnEventTypeWeDoNotHandle_SucceedsWithoutPublishing()
    {
        Verifies(new Event
        {
            Id = $"evt_{Guid.NewGuid():N}",
            Type = "customer.updated",
            Data = new EventData { Object = new Customer { Id = "cus_1" } },
        });

        var result = await _fixture.CreateWebhookService().HandleAsync("{}", Signature);

        result.IsSuccess.Should().BeTrue();
        _fixture.EventPublisher.VerifyNoOtherCalls();
        // Not recorded either: only events we actually acted on are worth de-duplicating.
        _fixture.DbContext.StripeEvents.Should().BeEmpty();
    }

    public void Dispose() => _fixture.Dispose();
}
