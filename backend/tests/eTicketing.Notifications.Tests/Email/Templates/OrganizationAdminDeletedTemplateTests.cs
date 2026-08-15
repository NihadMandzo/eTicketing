using eTicketing.Notifications.Email.Templates;
using FluentAssertions;

namespace eTicketing.Notifications.Tests.Email.Templates;

public class OrganizationAdminDeletedTemplateTests
{
    [Fact]
    public void Render_ReturnsSubjectAndHtmlContainingReasonAndDeletedAdminName()
    {
        var (subject, html) = OrganizationAdminDeletedTemplate.Render(new OrganizationAdminDeletedData(
            "Acme Events", "John Smith", "Kršenje internih pravila organizacije."));

        subject.Should().Contain("Acme Events");
        html.Should().Contain("John Smith");
        html.Should().Contain("Kršenje internih pravila organizacije.");
    }
}
