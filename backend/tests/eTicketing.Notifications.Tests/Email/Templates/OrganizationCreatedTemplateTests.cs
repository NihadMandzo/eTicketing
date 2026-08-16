using eTicketing.Notifications.Email.Templates;
using FluentAssertions;

namespace eTicketing.Notifications.Tests.Email.Templates;

public class OrganizationCreatedTemplateTests
{
    [Fact]
    public void Render_ReturnsSubjectAndHtmlContainingOrganizationNameAndLoginUrl()
    {
        var (subject, html) = OrganizationCreatedTemplate.Render(
            new OrganizationCreatedData("Acme Events", "http://localhost:4200/prijava"));

        subject.Should().Contain("Acme Events");
        html.Should().Contain("Acme Events");
        html.Should().Contain("http://localhost:4200/prijava");
    }

    [Fact]
    public void Render_EncodesHtmlSpecialCharactersInOrganizationName()
    {
        var (subject, html) = OrganizationCreatedTemplate.Render(
            new OrganizationCreatedData("<script>alert(1)</script>", "http://localhost:4200/prijava"));

        // The real Subject header is deliberately left as plain text, never HTML-encoded.
        subject.Should().Contain("<script>alert(1)</script>");
        // ...but the same value must come out encoded everywhere it lands in the HTML body,
        // including the <title> tag (proves the EmailLayout.Wrap fix, not just the body).
        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt;");
    }
}
