using eTicketing.Payment.Business.Payments.Gateways;
using FluentAssertions;

namespace eTicketing.Payment.Business.Tests.Payments.Gateways;

/// <summary>
/// The offline gateway is not a toy: docs/arhitektura-migracija-mikroservisi-eda.md section 9 names
/// Stripe the second thing to sacrifice and this the documented fallback, so PAYMENT_PROVIDER=Mock
/// has to keep producing a working declined-card path with no Stripe account at all.
/// </summary>
public class MockPaymentGatewayTests
{
    private readonly MockPaymentGateway _sut = new();

    private static GatewayCaptureRequest Capture(string? last4) =>
        new("pi_mock_1", "order-1", Guid.NewGuid(), 100m, "eur", last4);

    [Fact]
    public void Name_IsMock_AndThereIsNoPublishableKey()
    {
        _sut.Name.Should().Be(PaymentProviderNames.Mock);
        // Null is how the clients know to render the plain card form instead of a payment SDK.
        _sut.PublishableKey.Should().BeNull();
    }

    [Fact]
    public async Task CreateIntentAsync_ReturnsAnIntentAndAClientSecret()
    {
        var result = await _sut.CreateIntentAsync(new GatewayIntentRequest(
            100m, "eur", "order-1", Guid.NewGuid(), "kupac@example.com", "test",
            new Dictionary<string, string>()));

        result.IntentId.Should().StartWith("pi_mock_");
        result.ClientSecret.Should().NotBeNullOrEmpty();
        result.AmountMinor.Should().Be(10_000);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("4242")]
    [InlineData(null)]
    public async Task CaptureAsync_ForAnyCardNotEndingIn0000_Succeeds(string? last4)
    {
        var result = await _sut.CaptureAsync(Capture(last4));

        result.Status.Should().Be(GatewayPaymentStatus.Succeeded);
        result.FailureCode.Should().BeNull();
    }

    [Fact]
    public async Task CaptureAsync_ForACardEndingIn0000_Declines()
    {
        var result = await _sut.CaptureAsync(Capture(MockPaymentGateway.DecliningLast4));

        result.Status.Should().Be(GatewayPaymentStatus.Failed);
        result.FailureCode.Should().Be("card_declined");
    }

    [Fact]
    public async Task ConfirmSubscriptionAsync_ForACardEndingIn0000_Declines()
    {
        var result = await _sut.ConfirmSubscriptionAsync("sub_mock_1", MockPaymentGateway.DecliningLast4);

        result.Status.Should().Be(GatewayPaymentStatus.Failed);
        result.FailureCode.Should().Be("card_declined");
    }

    [Fact]
    public async Task ConfirmSubscriptionAsync_ForAnyOtherCard_SucceedsWithAOneMonthPeriod()
    {
        var result = await _sut.ConfirmSubscriptionAsync("sub_mock_1", "4242");

        result.Status.Should().Be(GatewayPaymentStatus.Succeeded);
        result.CurrentPeriodEnd.Should().Be(result.CurrentPeriodStart.AddMonths(1).AddDays(-1));
    }
}
