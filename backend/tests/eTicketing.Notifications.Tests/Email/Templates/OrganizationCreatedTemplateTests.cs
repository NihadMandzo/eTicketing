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
}
