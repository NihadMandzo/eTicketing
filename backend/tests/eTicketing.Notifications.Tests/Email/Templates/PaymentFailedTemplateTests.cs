using eTicketing.Notifications.Email.Templates;
using FluentAssertions;

namespace eTicketing.Notifications.Tests.Email.Templates;

public class PaymentFailedTemplateTests
{
    private static readonly DateTime FailedAt = new(2026, 9, 1, 14, 30, 0, DateTimeKind.Utc);

    private static PaymentFailedData Data(
        string reasonCode = "card_declined",
        Guid? orderId = null,
        string? sectorName = "VIP",
        decimal? amount = 50m) =>
        new(orderId ?? Guid.Parse("a1b2c3d4-0000-0000-0000-000000000000"), sectorName, amount, reasonCode, FailedAt);

    [Fact]
    public void Render_SaysNothingWasCharged()
    {
        // The single most important line in this email. A "payment failed" notice that does not say
        // so reads as "we took your money and gave you nothing", which is precisely backwards.
        var (_, html) = PaymentFailedTemplate.Render(Data());

        html.Should().Contain("Novac vam nije naplaćen");
    }

    [Fact]
    public void Render_DoesNotOfferARefundOrPromiseARetry()
    {
        // Nothing was captured — the purchase path cancels the authorization and releases the hold
        // before this event exists — so both would be lies, and a refund line in particular would
        // have buyers waiting for money that never left their account.
        var (_, html) = PaymentFailedTemplate.Render(Data());

        html.Should().NotContain("povrat");
        html.Should().NotContain("Povrat");
        html.Should().NotContain("pokušat ćemo ponovo");
    }

    [Fact]
    public void Render_ShowsTheOrderDetailsSoTwoAttemptsCanBeToldApart()
    {
        var orderId = Guid.Parse("abcdef12-3456-7890-abcd-ef1234567890");

        var (_, html) = PaymentFailedTemplate.Render(Data(orderId: orderId, sectorName: "Parter", amount: 120.50m));

        html.Should().Contain("Parter");
        html.Should().Contain("120.50 KM");
        html.Should().Contain("ABCDEF12");
        html.Should().Contain("01.09.2026. 14:30");
    }

    [Theory]
    [InlineData("insufficient_funds", "nema dovoljno sredstava")]
    [InlineData("expired_card", "istekla")]
    [InlineData("incorrect_cvc", "CVC")]
    [InlineData("authentication_required", "3D Secure")]
    [InlineData("processing_error", "greške pri obradi")]
    public void Render_TranslatesTheProvidersReasonCode(string reasonCode, string expectedFragment)
    {
        var (_, html) = PaymentFailedTemplate.Render(Data(reasonCode));

        html.Should().Contain(expectedFragment);
        // Never the raw code: it is English, it is jargon, and it is not what the buyer needs.
        html.Should().NotContain(reasonCode);
    }

    [Theory]
    [InlineData("lost_card")]
    [InlineData("stolen_card")]
    [InlineData("fraudulent")]
    [InlineData("pickup_card")]
    public void Render_ForACodeThatDescribesTheCardholder_SaysOnlyThatTheBankDeclined(string reasonCode)
    {
        // The address on an order is not necessarily the cardholder's. Telling whoever is reading
        // that a card has been reported stolen confirms something for them that they may be exactly
        // the wrong person to learn.
        var (_, html) = PaymentFailedTemplate.Render(Data(reasonCode));

        html.Should().Contain("Banka je odbila plaćanje");
        html.Should().NotContain(reasonCode);
        html.Should().NotContain("ukraden");
        html.Should().NotContain("prijavljen");
    }

    [Fact]
    public void Render_ForAnUnknownReasonCode_FallsBackRatherThanPrintingIt()
    {
        // Providers add decline codes without asking. An unmapped one must never reach the inbox.
        var (_, html) = PaymentFailedTemplate.Render(Data("some_brand_new_code_2027"));

        html.Should().Contain("Banka je odbila plaćanje");
        html.Should().NotContain("some_brand_new_code_2027");
    }

    [Fact]
    public void Render_ForAnEventWithoutOrderDetails_StillRendersWithoutEmptyRows()
    {
        // The deploy window: an event published by the previous version carries none of these. A
        // slightly vaguer email beats a dead-lettered one.
        var (_, html) = PaymentFailedTemplate.Render(
            new PaymentFailedData(null, null, null, "card_declined", FailedAt));

        html.Should().NotContain("Sektor");
        html.Should().NotContain("Iznos");
        html.Should().NotContain("Broj narudžbe");
        html.Should().Contain("01.09.2026. 14:30");
    }

    [Fact]
    public void Render_EncodesHtmlSpecialCharactersInTheSectorName()
    {
        var (_, html) = PaymentFailedTemplate.Render(Data(sectorName: "<script>alert(1)</script>"));

        html.Should().NotContain("<script>");
        html.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void Render_UsesAStableSubjectThatDoesNotLeakTheReason()
    {
        // Subject lines are visible in notification previews on a lock screen.
        var (subject, _) = PaymentFailedTemplate.Render(Data("stolen_card"));

        subject.Should().Be("Plaćanje nije uspjelo — eKarta");
    }

    [Fact]
    public void Render_TellsTheBuyerTheSeatsWereReleased()
    {
        // Otherwise a buyer who just failed to pay reasonably assumes their reservation is still
        // waiting, and comes back in ten minutes expecting it.
        var (_, html) = PaymentFailedTemplate.Render(Data());

        html.Should().Contain("Rezervacija je oslobođena");
    }

    [Fact]
    public void Render_FormatsTheAmountWithTwoDecimalsRegardlessOfCulture()
    {
        var (_, html) = PaymentFailedTemplate.Render(Data(amount: 7m));

        html.Should().Contain("7.00 KM");
    }
}
