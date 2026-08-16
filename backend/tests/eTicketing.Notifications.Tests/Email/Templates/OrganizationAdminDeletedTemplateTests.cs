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

    [Fact]
    public void Render_EncodesHtmlSpecialCharactersInDeletedAdminNameAndReason()
    {
        // Reason is SuperAdmin-entered free text (up to 500 chars, no character restriction) and
        // this email goes to the organization's own contact address — a third party who never
        // had a chance to sanitize it themselves.
        var (subject, html) = OrganizationAdminDeletedTemplate.Render(new OrganizationAdminDeletedData(
            "<script>alert(1)</script>", "<img src=x onerror=alert(2)>", "<b>razlog</b>"));

        subject.Should().Contain("<script>alert(1)</script>");
        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().NotContain("<img src=x onerror=alert(2)>");
        html.Should().NotContain("<b>razlog</b>");
        html.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt;");
        html.Should().Contain("&lt;img src=x onerror=alert(2)&gt;");
        html.Should().Contain("&lt;b&gt;razlog&lt;/b&gt;");
    }
}
