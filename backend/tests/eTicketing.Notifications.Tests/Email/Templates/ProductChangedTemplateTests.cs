using eTicketing.Notifications.Email.Templates;
using FluentAssertions;

namespace eTicketing.Notifications.Tests.Email.Templates;

public class ProductChangedTemplateTests
{
    [Fact]
    public void Render_IncludesProductNameAndEveryChangedField()
    {
        var (subject, html) = ProductChangedTemplate.Render(new ProductChangedData("Ljetni Festival",
        [
            new ProductChangeLine("Datum i vrijeme", "01.09.2026. 20:00", "02.09.2026. 20:00"),
            new ProductChangeLine("Grad", "Sarajevo", "Mostar"),
        ]));

        subject.Should().Contain("Ljetni Festival");
        html.Should().Contain("Datum i vrijeme");
        html.Should().Contain("01.09.2026. 20:00");
        html.Should().Contain("02.09.2026. 20:00");
        html.Should().Contain("Grad");
        html.Should().Contain("Mostar");
    }

    [Fact]
    public void Render_ReassuresThatTheTicketRemainsValid()
    {
        // The single most important line in this email: a changed date must not read as a
        // cancellation.
        var (_, html) = ProductChangedTemplate.Render(new ProductChangedData("Ljetni Festival",
            [new ProductChangeLine("Naziv", "Staro", "Novo")]));

        html.Should().Contain("Vaša ulaznica i dalje vrijedi");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Render_ForAClearedField_ShowsAnExplicitEmptyMarker(string? newValue)
    {
        var (_, html) = ProductChangedTemplate.Render(new ProductChangedData("Ljetni Festival",
            [new ProductChangeLine("Opis", "Stari opis", newValue)]));

        html.Should().Contain("(prazno)");
    }

    [Fact]
    public void Render_EncodesHtmlSpecialCharactersInFieldValues()
    {
        var (_, html) = ProductChangedTemplate.Render(new ProductChangedData("Ljetni Festival",
            [new ProductChangeLine("Opis", "Staro", "<script>alert(1)</script>")]));

        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt;");
    }

    [Fact]
    public void Render_EncodesHtmlSpecialCharactersInProductName()
    {
        var (subject, html) = ProductChangedTemplate.Render(new ProductChangedData("<b>Festival</b>",
            [new ProductChangeLine("Naziv", "Staro", "Novo")]));

        // The real Subject header is deliberately left as plain text, never HTML-encoded.
        subject.Should().Contain("<b>Festival</b>");
        html.Should().NotContain("<b>Festival</b>");
        html.Should().Contain("&lt;b&gt;Festival&lt;/b&gt;");
    }
}
