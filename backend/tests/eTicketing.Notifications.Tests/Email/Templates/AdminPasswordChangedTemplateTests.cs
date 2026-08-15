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
}
