using eTicketing.Notifications.Email.Templates;
using FluentAssertions;

namespace eTicketing.Notifications.Tests.Email.Templates;

public class PasswordResetTemplateTests
{
    [Fact]
    public void Render_ReturnsSubjectAndHtmlContainingResetLink()
    {
        const string resetLink = "http://localhost:4200/resetovanje-lozinke?token=raw-token-value";

        var (subject, html) = PasswordResetTemplate.Render(new PasswordResetData("Jane", resetLink));

        subject.Should().NotBeNullOrWhiteSpace();
        html.Should().Contain(resetLink);
        html.Should().Contain("Jane");
    }

    [Fact]
    public void Render_EncodesHtmlSpecialCharactersInFirstName()
    {
        var (_, html) = PasswordResetTemplate.Render(new PasswordResetData(
            "<script>alert(1)</script>", "http://localhost:4200/resetovanje-lozinke?token=raw-token-value"));

        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt;");
    }
}
