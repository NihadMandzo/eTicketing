using eTicketing.Notifications.Email;
using eTicketing.Notifications.Email.Templates;
using FluentAssertions;

namespace eTicketing.Notifications.Tests.Email;

public class EmailMessageBuilderTests
{
    [Fact]
    public void Build_WithoutWithTo_ThrowsInvalidOperationException()
    {
        var builder = new EmailMessageBuilder()
            .WithTemplate(EmailTemplate.AdminPasswordChanged, new AdminPasswordChangedData("Jane"));

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Build_WithoutWithTemplate_ThrowsInvalidOperationException()
    {
        var builder = new EmailMessageBuilder().WithTo("jane@example.com");

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void WithTemplate_SetsSubjectAndHtmlBody()
    {
        var message = new EmailMessageBuilder()
            .WithTo("jane@example.com", "Jane Doe")
            .WithTemplate(EmailTemplate.AdminPasswordChanged, new AdminPasswordChangedData("Jane"))
            .Build();

        message.ToEmail.Should().Be("jane@example.com");
        message.ToName.Should().Be("Jane Doe");
        message.Subject.Should().NotBeNullOrWhiteSpace();
        message.HtmlBody.Should().Contain("Jane");
    }

    [Fact]
    public void WithSubject_OverridesTemplateDefaultSubject()
    {
        var message = new EmailMessageBuilder()
            .WithTo("jane@example.com")
            .WithSubject("Custom Subject")
            .WithTemplate(EmailTemplate.AdminPasswordChanged, new AdminPasswordChangedData("Jane"))
            .Build();

        message.Subject.Should().Be("Custom Subject");
    }

    [Fact]
    public void Build_WithNoAttachments_ReturnsAnEmptyList()
    {
        // Never null — BrevoEmailSender inspects Count to decide whether to send the attachment
        // array at all.
        var message = new EmailMessageBuilder()
            .WithTo("jane@example.com")
            .WithTemplate(EmailTemplate.AdminPasswordChanged, new AdminPasswordChangedData("Jane"))
            .Build();

        message.Attachments.Should().BeEmpty();
    }

    [Fact]
    public void WithAttachment_IsRepeatable_SoOneOrderCanCarrySeveralTicketPdfs()
    {
        var message = new EmailMessageBuilder()
            .WithTo("jane@example.com")
            .WithTemplate(EmailTemplate.AdminPasswordChanged, new AdminPasswordChangedData("Jane"))
            .WithAttachment("ulaznica-1.pdf", "%PDF-1.4 first"u8.ToArray())
            .WithAttachment("ulaznica-2.pdf", "%PDF-1.4 second"u8.ToArray())
            .Build();

        message.Attachments.Should().HaveCount(2);
        message.Attachments[0].Name.Should().Be("ulaznica-1.pdf");
        message.Attachments[1].Content.Should().Equal("%PDF-1.4 second"u8.ToArray());
    }

    [Fact]
    public void WithTemplate_WrongDataType_ThrowsInvalidCastException()
    {
        var act = () => new EmailMessageBuilder()
            .WithTo("jane@example.com")
            .WithTemplate(EmailTemplate.AdminPasswordChanged, new VerificationEmailData("Jane", "ABC123"));

        act.Should().Throw<InvalidCastException>();
    }
}
