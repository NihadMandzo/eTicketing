using eTicketing.Payment.Business.Payments;
using eTicketing.Payment.Business.Tests.TestFixtures;
using eTicketing.Payment.Data.Entities;
using FluentAssertions;

namespace eTicketing.Payment.Business.Tests.Payments;

public class PaymentServiceTests : IDisposable
{
    private readonly PaymentTestContext _fixture = new();
    private readonly IPaymentService _sut;

    public PaymentServiceTests() => _sut = _fixture.CreatePaymentService();

    [Fact]
    public async Task ChargeAsync_CardNotEndingIn0000_ReturnsSucceeded()
    {
        var result = await _sut.ChargeAsync(new ChargeRequest { Amount = 100, OrderRef = "order-1", CardNumberLast4 = "1234" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PaymentStatus.Succeeded);
        result.Value.Amount.Should().Be(100);
        result.Value.OrderRef.Should().Be("order-1");
    }

    [Fact]
    public async Task ChargeAsync_CardEndingIn0000_ReturnsFailed()
    {
        var result = await _sut.ChargeAsync(new ChargeRequest { Amount = 50, OrderRef = "order-2", CardNumberLast4 = "0000" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public async Task ChargeAsync_AlwaysPersistsPaymentRow()
    {
        await _sut.ChargeAsync(new ChargeRequest { Amount = 75, OrderRef = "order-3", CardNumberLast4 = "0000" });
        await _sut.ChargeAsync(new ChargeRequest { Amount = 25, OrderRef = "order-4", CardNumberLast4 = "4242" });

        var all = _fixture.PaymentRepository.Query().ToList();
        all.Should().HaveCount(2);
        all.Should().Contain(p => p.OrderRef == "order-3" && p.Status == PaymentStatus.Failed);
        all.Should().Contain(p => p.OrderRef == "order-4" && p.Status == PaymentStatus.Succeeded);
    }

    public void Dispose() => _fixture.Dispose();
}
