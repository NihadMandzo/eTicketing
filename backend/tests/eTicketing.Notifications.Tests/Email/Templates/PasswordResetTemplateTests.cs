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
}
