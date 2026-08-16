using eTicketing.Notifications.Email.Templates;
using FluentAssertions;

namespace eTicketing.Notifications.Tests.Email.Templates;

public class AdminPasswordChangedTemplateTests
{
    [Fact]
    public void Render_ReturnsSubjectAndHtmlContainingFirstName()
    {
        var (subject, html) = AdminPasswordChangedTemplate.Render(new AdminPasswordChangedData("Jane"));

        subject.Should().NotBeNullOrWhiteSpace();
        html.Should().Contain("Jane");
    }

    [Fact]
    public void Render_EncodesHtmlSpecialCharactersInFirstName()
    {
        var (_, html) = AdminPasswordChangedTemplate.Render(new AdminPasswordChangedData("<script>alert(1)</script>"));

        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt;");
    }
}
