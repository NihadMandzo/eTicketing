using eTicketing.Payment.Business.Payments;
using eTicketing.Payment.Business.Payments.Gateways;
using eTicketing.Payment.Business.Tests.TestFixtures;
using eTicketing.Payment.Data.Entities;
using FluentAssertions;
using Moq;

namespace eTicketing.Payment.Business.Tests.Payments;

/// <summary>
/// The provider is mocked through IPaymentGateway, so nothing here touches the network.
///
/// The single most important behaviour asserted below is the split between a decline and an outage:
/// a declined card is a SUCCESSFUL Result carrying Status = Failed, while an unreachable provider is
/// a Result failure. eTicketing.Ticketing's PurchaseService branches on exactly that to answer 400
/// versus 503, so collapsing the two would silently turn "your card was declined" into "try again
/// later" and leave holds released for the wrong reason.
/// </summary>
public class PaymentServiceTests : IDisposable
{
    private readonly PaymentTestContext _fixture = new();
    private readonly IPaymentService _sut;

    private static readonly Guid Buyer = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public PaymentServiceTests() => _sut = _fixture.CreatePaymentService();

    private static CreateIntentRequest IntentRequest(string orderRef = "order-1", decimal amount = 100m, bool subscription = false) => new()
    {
        Amount = amount,
        OrderRef = orderRef,
        UserId = Buyer,
        CustomerEmail = "kupac@example.com",
        Description = "eKarta — VIP",
        HoldRef = "hold-1",
        SectorId = Guid.NewGuid(),
        IsSubscription = subscription,
        SubscriptionProductName = subscription ? "A-12 — mjesečna rezervacija" : null,
    };

    private void GatewayCreatesIntent(string intentId = "pi_1") =>
        _fixture.Gateway
            .Setup(g => g.CreateIntentAsync(It.IsAny<GatewayIntentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GatewayIntentRequest r, CancellationToken _) => new GatewayIntentResult(
                intentId, $"{intentId}_secret", GatewayPaymentStatus.RequiresPaymentMethod,
                MoneyConverter.ToMinorUnits(r.Amount), r.Currency));

    private void GatewayCaptures(GatewayPaymentStatus status, string? failureCode = null) =>
        _fixture.Gateway
            .Setup(g => g.CaptureAsync(It.IsAny<GatewayCaptureRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GatewayCaptureRequest r, CancellationToken _) => new GatewayCaptureResult(
                status, failureCode, MoneyConverter.ToMinorUnits(r.ExpectedAmount), r.ExpectedCurrency));

    [Fact]
    public async Task CreateIntentAsync_PersistsPendingPaymentAndReturnsClientSecret()
    {
        GatewayCreatesIntent();

        var result = await _sut.CreateIntentAsync(IntentRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.ClientSecret.Should().Be("pi_1_secret");
        result.Value.Provider.Should().Be(PaymentProviderNames.Stripe);
        result.Value.Currency.Should().Be("eur");

        var stored = _fixture.PaymentRepository.Query().Single();
        stored.Status.Should().Be(PaymentStatus.Pending);
        stored.OrderRef.Should().Be("order-1");
        stored.ProviderPaymentIntentId.Should().Be("pi_1");
        stored.UserId.Should().Be(Buyer);
    }

    /// <summary>The provider is idempotent on OrderRef, so a replayed create returns the same intent.
    /// This service must then update the existing row rather than insert a second one, or the unique
    /// OrderRef index would reject it and a retry would look like a hard failure.</summary>
    [Fact]
    public async Task CreateIntentAsync_CalledTwiceWithSameOrderRef_KeepsExactlyOneRow()
    {
        GatewayCreatesIntent();

        await _sut.CreateIntentAsync(IntentRequest());
        var second = await _sut.CreateIntentAsync(IntentRequest());

        second.IsSuccess.Should().BeTrue();
        _fixture.PaymentRepository.Query().Count(p => p.OrderRef == "order-1").Should().Be(1);
    }

    [Fact]
    public async Task CreateIntentAsync_WhenProviderIsUnavailable_ReturnsFailureResult()
    {
        _fixture.Gateway
            .Setup(g => g.CreateIntentAsync(It.IsAny<GatewayIntentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PaymentGatewayUnavailableException("down"));

        var result = await _sut.CreateIntentAsync(IntentRequest());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("payment.provider_unavailable");
        // ErrorType.Failure is what ResultExtensions maps to 503, which HttpPaymentClient reads back
        // as "unavailable" and PurchaseService answers by releasing the hold.
        result.Error.Type.Should().Be(eTicketing.Contracts.Results.ErrorType.Failure);
    }

    [Fact]
    public async Task CaptureAsync_WhenGatewaySucceeds_MarksPaymentSucceeded()
    {
        GatewayCreatesIntent();
        GatewayCaptures(GatewayPaymentStatus.Succeeded);
        await _sut.CreateIntentAsync(IntentRequest());

        var result = await _sut.CaptureAsync(new CapturePaymentRequest
        {
            IntentId = "pi_1", OrderRef = "order-1", UserId = Buyer, ExpectedAmount = 100m,
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PaymentStatus.Succeeded);
        _fixture.PaymentRepository.Query().Single().Status.Should().Be(PaymentStatus.Succeeded);
    }

    /// <summary>The contract PurchaseService depends on: a decline is a normal answer, not an error.</summary>
    [Fact]
    public async Task CaptureAsync_WhenGatewayDeclines_ReturnsSuccessfulResultWithFailedStatus()
    {
        GatewayCreatesIntent();
        GatewayCaptures(GatewayPaymentStatus.Failed, "insufficient_funds");
        await _sut.CreateIntentAsync(IntentRequest());

        var result = await _sut.CaptureAsync(new CapturePaymentRequest
        {
            IntentId = "pi_1", OrderRef = "order-1", UserId = Buyer, ExpectedAmount = 100m,
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PaymentStatus.Failed);
        // Carried through so the clients can show a specific Bosnian message rather than one generic
        // "payment failed".
        result.Value.FailureCode.Should().Be("insufficient_funds");
    }

    [Fact]
    public async Task CaptureAsync_ForUnknownOrderRef_ReturnsNotFound()
    {
        var result = await _sut.CaptureAsync(new CapturePaymentRequest
        {
            IntentId = "pi_1", OrderRef = "does-not-exist", UserId = Buyer, ExpectedAmount = 100m,
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("payment.not_found");
    }

    /// <summary>A caller presenting someone else's intent id is refused from our OWN records, before
    /// the provider is ever consulted -- the strict gateway mock proves no call was made.</summary>
    [Fact]
    public async Task CaptureAsync_WithIntentIdThatIsNotOnTheOrder_FailsWithoutCallingTheProvider()
    {
        GatewayCreatesIntent();
        await _sut.CreateIntentAsync(IntentRequest());

        var result = await _sut.CaptureAsync(new CapturePaymentRequest
        {
            IntentId = "pi_someone_else", OrderRef = "order-1", UserId = Buyer, ExpectedAmount = 100m,
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PaymentStatus.Failed);
        result.Value.FailureCode.Should().Be("intent_mismatch");
        _fixture.Gateway.Verify(
            g => g.CaptureAsync(It.IsAny<GatewayCaptureRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CaptureAsync_WithADifferentBuyer_FailsWithoutCallingTheProvider()
    {
        GatewayCreatesIntent();
        await _sut.CreateIntentAsync(IntentRequest());

        var result = await _sut.CaptureAsync(new CapturePaymentRequest
        {
            IntentId = "pi_1", OrderRef = "order-1", UserId = Guid.NewGuid(), ExpectedAmount = 100m,
        });

        result.Value!.Status.Should().Be(PaymentStatus.Failed);
        result.Value.FailureCode.Should().Be("intent_user_mismatch");
        _fixture.Gateway.Verify(
            g => g.CaptureAsync(It.IsAny<GatewayCaptureRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Guards against a client that got an intent for one price trying to settle a bigger
    /// order with it.</summary>
    [Fact]
    public async Task CaptureAsync_WithAnAmountThatIsNotTheOrderTotal_FailsWithoutCallingTheProvider()
    {
        GatewayCreatesIntent();
        await _sut.CreateIntentAsync(IntentRequest());

        var result = await _sut.CaptureAsync(new CapturePaymentRequest
        {
            IntentId = "pi_1", OrderRef = "order-1", UserId = Buyer, ExpectedAmount = 5m,
        });

        result.Value!.Status.Should().Be(PaymentStatus.Failed);
        result.Value.FailureCode.Should().Be("intent_amount_mismatch");
        _fixture.Gateway.Verify(
            g => g.CaptureAsync(It.IsAny<GatewayCaptureRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Ticketing's Polly retry can re-send a capture whose response was lost. The money is
    /// already taken, so this must report the original outcome, not capture again.</summary>
    [Fact]
    public async Task CaptureAsync_CalledTwice_CapturesOnlyOnce()
    {
        GatewayCreatesIntent();
        GatewayCaptures(GatewayPaymentStatus.Succeeded);
        await _sut.CreateIntentAsync(IntentRequest());

        var request = new CapturePaymentRequest
        {
            IntentId = "pi_1", OrderRef = "order-1", UserId = Buyer, ExpectedAmount = 100m,
        };

        var first = await _sut.CaptureAsync(request);
        var second = await _sut.CaptureAsync(request);

        first.Value!.Status.Should().Be(PaymentStatus.Succeeded);
        second.Value!.Status.Should().Be(PaymentStatus.Succeeded);
        second.Value.Id.Should().Be(first.Value.Id);
        _fixture.Gateway.Verify(
            g => g.CaptureAsync(It.IsAny<GatewayCaptureRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CaptureAsync_WhenProviderIsUnavailable_ReturnsFailureResult()
    {
        GatewayCreatesIntent();
        await _sut.CreateIntentAsync(IntentRequest());
        _fixture.Gateway
            .Setup(g => g.CaptureAsync(It.IsAny<GatewayCaptureRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PaymentGatewayUnavailableException("down"));

        var result = await _sut.CaptureAsync(new CapturePaymentRequest
        {
            IntentId = "pi_1", OrderRef = "order-1", UserId = Buyer, ExpectedAmount = 100m,
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("payment.provider_unavailable");
    }

    private void GatewayCreatesSubscription(string subscriptionId = "sub_1") =>
        _fixture.Gateway
            .Setup(g => g.CreateSubscriptionAsync(It.IsAny<GatewaySubscriptionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewaySubscriptionResult(
                subscriptionId, $"{subscriptionId}_secret", null, GatewayPaymentStatus.RequiresPaymentMethod,
                null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)));

    [Fact]
    public async Task CreateIntentAsync_ForSubscription_StoresSubscriptionReferenceAndCreatesCustomerOnce()
    {
        _fixture.Gateway
            .Setup(g => g.CreateCustomerAsync(Buyer, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cus_1");
        GatewayCreatesSubscription();

        var first = await _sut.CreateIntentAsync(IntentRequest("order-sub-1", 60m, subscription: true));
        await _sut.CreateIntentAsync(IntentRequest("order-sub-2", 60m, subscription: true));

        first.IsSuccess.Should().BeTrue();
        first.Value!.SubscriptionReference.Should().Be("sub_1");

        _fixture.PaymentRepository.Query()
            .Single(p => p.OrderRef == "order-sub-1").ProviderSubscriptionId.Should().Be("sub_1");

        // A buyer with two parking subscriptions must be ONE provider customer with one saved card,
        // so the second subscription reuses the stored mapping instead of creating another.
        _fixture.Gateway.Verify(
            g => g.CreateCustomerAsync(Buyer, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _fixture.StripeCustomerRepository.Query().Count(c => c.UserId == Buyer).Should().Be(1);
    }

    [Fact]
    public async Task CreateIntentAsync_ForSubscriptionWithoutAProductName_ReturnsValidationError()
    {
        var request = IntentRequest("order-sub-3", 60m, subscription: true) with { SubscriptionProductName = null };

        var result = await _sut.CreateIntentAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("payment.subscription_name_required");
    }

    /// <summary>The Subscription row in Ticketing is stamped with whatever period comes back here, so
    /// it has to be the provider's, not one computed locally.</summary>
    [Fact]
    public async Task ConfirmSubscriptionAsync_WhenPaid_ReportsTheProvidersBillingPeriod()
    {
        _fixture.Gateway
            .Setup(g => g.CreateCustomerAsync(Buyer, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cus_1");
        GatewayCreatesSubscription();
        _fixture.Gateway
            .Setup(g => g.ConfirmSubscriptionAsync("sub_1", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewaySubscriptionResult(
                "sub_1", null, null, GatewayPaymentStatus.Succeeded, null,
                new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)));

        await _sut.CreateIntentAsync(IntentRequest("order-sub-4", 60m, subscription: true));

        var result = await _sut.ConfirmSubscriptionAsync(new ConfirmSubscriptionRequest
        {
            SubscriptionReference = "sub_1", OrderRef = "order-sub-4", UserId = Buyer, ExpectedAmount = 60m,
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PaymentStatus.Succeeded);
        result.Value.CurrentPeriodStart.Should().Be(new DateOnly(2026, 9, 1));
        result.Value.CurrentPeriodEnd.Should().Be(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public async Task ConfirmSubscriptionAsync_WithAReferenceThatIsNotOnTheOrder_Fails()
    {
        _fixture.Gateway
            .Setup(g => g.CreateCustomerAsync(Buyer, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cus_1");
        GatewayCreatesSubscription();
        await _sut.CreateIntentAsync(IntentRequest("order-sub-5", 60m, subscription: true));

        var result = await _sut.ConfirmSubscriptionAsync(new ConfirmSubscriptionRequest
        {
            SubscriptionReference = "sub_someone_else", OrderRef = "order-sub-5", UserId = Buyer, ExpectedAmount = 60m,
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PaymentStatus.Failed);
        result.Value.FailureCode.Should().Be("subscription_mismatch");
    }

    public void Dispose() => _fixture.Dispose();
}
