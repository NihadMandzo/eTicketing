using eTicketing.Notifications.Email.Templates;
using FluentAssertions;

namespace eTicketing.Notifications.Tests.Email.Templates;

public class TicketsReadyTemplateTests
{
    [Fact]
    public void Render_IncludesProductNameValidityCityAndTotal()
    {
        var (subject, html) = TicketsReadyTemplate.Render(Data());

        subject.Should().Contain("Ljetni Festival");
        html.Should().Contain("Ljetni Festival");
        html.Should().Contain("01.09.2026. 20:00");
        html.Should().Contain("Sarajevo");
        // Formatted with the same format string the template uses, so the assertion holds whatever
        // decimal separator the current culture picks.
        html.Should().Contain($"{100m:0.00} KM");
    }

    [Fact]
    public void Render_ListsEveryTicketWithItsCodeSectorAndPrice()
    {
        var (_, html) = TicketsReadyTemplate.Render(Data());

        html.Should().Contain("A1B2C3D4");
        html.Should().Contain("E5F6A7B8");
        html.Should().Contain("VIP · Odrasli");
        html.Should().Contain("VIP · Djeca");
    }

    [Fact]
    public void Render_ForASingleTicket_UsesSingularAttachmentWording()
    {
        var (_, html) = TicketsReadyTemplate.Render(Data() with
        {
            Tickets = [new TicketsReadyLine("A1B2C3D4", "VIP", null, 50)],
            TotalPaid = 50,
        });

        html.Should().Contain("Ulaznica je u prilogu");
        html.Should().NotContain("Sve ulaznice");
    }

    [Fact]
    public void Render_ForSeveralTickets_SaysHowManyAreAttached()
    {
        var (_, html) = TicketsReadyTemplate.Render(Data());

        html.Should().Contain("Sve ulaznice (2)");
    }

    [Fact]
    public void Render_ForATicketWithNoTypeTier_OmitsTheSeparator()
    {
        var (_, html) = TicketsReadyTemplate.Render(Data() with
        {
            Tickets = [new TicketsReadyLine("A1B2C3D4", "Parter", null, 50)],
        });

        html.Should().Contain("Parter");
        html.Should().NotContain("Parter ·");
    }

    [Fact]
    public void Render_EncodesHtmlSpecialCharactersInProductName()
    {
        var (subject, html) = TicketsReadyTemplate.Render(Data() with { ProductName = "<script>alert(1)</script>" });

        // The real Subject header is deliberately left as plain text, never HTML-encoded.
        subject.Should().Contain("<script>alert(1)</script>");
        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt;");
    }

    [Fact]
    public void Render_EncodesHtmlSpecialCharactersInSectorName()
    {
        // Sector names are organizer-supplied, so they're as untrusted as the product name.
        var (_, html) = TicketsReadyTemplate.Render(Data() with
        {
            Tickets = [new TicketsReadyLine("A1B2C3D4", "<b>VIP</b>", null, 50)],
        });

        html.Should().NotContain("<b>VIP</b>");
        html.Should().Contain("&lt;b&gt;VIP&lt;/b&gt;");
    }

    private static TicketsReadyData Data() => new(
        "Ljetni Festival",
        "01.09.2026. 20:00",
        "Sarajevo",
        100,
        [
            new TicketsReadyLine("A1B2C3D4", "VIP", "Odrasli", 60),
            new TicketsReadyLine("E5F6A7B8", "VIP", "Djeca", 40),
        ]);
}
