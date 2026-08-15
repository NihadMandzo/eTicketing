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
}
