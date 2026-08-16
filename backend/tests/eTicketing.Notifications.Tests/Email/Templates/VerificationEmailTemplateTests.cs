using eTicketing.Notifications.Email.Templates;
using FluentAssertions;

namespace eTicketing.Notifications.Tests.Email.Templates;

public class VerificationEmailTemplateTests
{
    [Fact]
    public void Render_ReturnsSubjectAndHtmlContainingCode()
    {
        var (subject, html) = VerificationEmailTemplate.Render(new VerificationEmailData("Jane", "ABC123"));

        subject.Should().NotBeNullOrWhiteSpace();
        html.Should().Contain("ABC123");
        html.Should().Contain("Jane");
    }

    [Fact]
    public void Render_EncodesHtmlSpecialCharactersInFirstName()
    {
        var (_, html) = VerificationEmailTemplate.Render(new VerificationEmailData("<script>alert(1)</script>", "ABC123"));

        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt;");
    }
}
