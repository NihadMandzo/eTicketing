using eTicketing.Payment.Business.Payments;
using eTicketing.Payment.Data.Entities;
using FluentAssertions;
using Mapster;
using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;

namespace eTicketing.Payment.Business.Tests.Payments;

/// <summary>
/// eTicketing.Payment had no Mapster reference at all until this phase, so this is the first test
/// here that exercises a mapping config rather than a service.
///
/// <para>The assertion that matters is the negative one. The entity carries the provider's intent
/// and subscription identifiers and the buyer's user id; the response — which crosses a service
/// boundary to eTicketing.Ticketing — carries none of them. Mapster drops unmatched source members
/// without a word, so nothing would complain if someone widened the response and this service
/// quietly started shipping provider references around the network.</para>
/// </summary>
public class PaymentMappingTests
{
    private static PaymentEntity Payment() => new()
    {
        Id = Guid.NewGuid(),
        Amount = 120.50m,
        Status = PaymentStatus.Succeeded,
        OrderRef = "a1b2c3d4",
        Currency = "eur",
        Provider = PaymentProvider.Stripe,
        UserId = Guid.NewGuid(),
        ProviderPaymentIntentId = "pi_test_123",
        ProviderSubscriptionId = "sub_test_123",
        FailureCode = null,
        CreatedAt = new DateTime(2026, 8, 24, 10, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void GlobalConfig_Compiles()
    {
        // Nothing else in this service would notice a broken config: PaymentMappingConfig is the
        // only one it has, and it is registered by a module initializer rather than by startup.
        var compile = () => TypeAdapterConfig.GlobalSettings.Compile();

        compile.Should().NotThrow();
    }

    [Fact]
    public void Payment_MapsEveryFieldTheCallerNeeds()
    {
        var payment = Payment();

        var response = payment.Adapt<PaymentResponse>();

        response.Id.Should().Be(payment.Id);
        response.Amount.Should().Be(120.50m);
        response.Status.Should().Be(PaymentStatus.Succeeded);
        response.OrderRef.Should().Be("a1b2c3d4");
        response.Currency.Should().Be("eur");
        response.CreatedAt.Should().Be(payment.CreatedAt);
    }

    [Fact]
    public void Payment_CarriesAFailureCodeWhenThereIsOne()
    {
        // The clients branch on this to show a specific Bosnian decline message rather than one
        // generic failure text, so a mapping that dropped it would degrade every decline at once.
        var payment = Payment();
        payment.Status = PaymentStatus.Failed;
        payment.FailureCode = "insufficient_funds";

        payment.Adapt<PaymentResponse>().FailureCode.Should().Be("insufficient_funds");
    }

    [Fact]
    public void PaymentResponse_CarriesNoProviderIdentifiers()
    {
        // Less an assertion about the mapping than about the shape it maps into: if any of these
        // ever appear on the response, Mapster will start filling them in silently.
        var members = typeof(PaymentResponse).GetProperties().Select(p => p.Name).ToList();

        members.Should().NotContain("ProviderPaymentIntentId");
        members.Should().NotContain("ProviderSubscriptionId");
        members.Should().NotContain("UserId");
        members.Should().NotContain("Provider");
    }
}
