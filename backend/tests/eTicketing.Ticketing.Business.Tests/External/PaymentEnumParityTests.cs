using eTicketing.Ticketing.Business.External;
using FluentAssertions;
using PaymentStatus = eTicketing.Payment.Data.Entities.PaymentStatus;

namespace eTicketing.Ticketing.Business.Tests.External;

/// <summary>
/// HttpPaymentClient.ChargeAsync deserializes Payment's PaymentResponse JSON straight into
/// PaymentChargeResponse — including its Status field, which System.Text.Json serializes as the
/// enum's numeric ordinal by default (no [JsonConverter] on either side). PaymentChargeStatus and
/// the real eTicketing.Payment.Data.Entities.PaymentStatus are two separately-declared enums that
/// must therefore keep identical member names in identical order; reordering or inserting a value
/// on either side would silently flip Succeeded/Failed across the service boundary with no compile
/// error. This test fails the build the moment that parity breaks.
/// </summary>
public class PaymentEnumParityTests
{
    [Fact]
    public void PaymentChargeStatus_StaysInLockstepWithRealPaymentStatus()
    {
        Enum.GetNames(typeof(PaymentChargeStatus)).Should().Equal(Enum.GetNames(typeof(PaymentStatus)));
    }
}
