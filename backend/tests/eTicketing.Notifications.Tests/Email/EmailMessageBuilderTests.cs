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
    public void WithTemplate_WrongDataType_ThrowsInvalidCastException()
    {
        var act = () => new EmailMessageBuilder()
            .WithTo("jane@example.com")
            .WithTemplate(EmailTemplate.AdminPasswordChanged, new VerificationEmailData("Jane", "ABC123"));

        act.Should().Throw<InvalidCastException>();
    }
}
