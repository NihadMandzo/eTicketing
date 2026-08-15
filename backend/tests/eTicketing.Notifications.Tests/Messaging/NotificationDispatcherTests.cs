using System.Text;
using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.Notifications.Email;
using eTicketing.Notifications.Messaging;
using eTicketing.Notifications.Options;
using eTicketing.Notifications.Sending;
using eTicketing.Notifications.Tests.TestSupport;
using FluentAssertions;
using Moq;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace eTicketing.Notifications.Tests.Messaging;

public class NotificationDispatcherTests
{
    private readonly Mock<IEmailSender> _emailSenderMock = new();
    private readonly ListLogger<NotificationDispatcher> _logger = new();
    private readonly NotificationDispatcher _sut;

    public NotificationDispatcherTests()
    {
        var frontendOptions = MsOptions.Create(new FrontendOptions { WebBaseUrl = "http://localhost:4200" });
        _sut = new NotificationDispatcher(_emailSenderMock.Object, frontendOptions, _logger);
    }

    private static ReadOnlyMemory<byte> Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value);

    [Fact]
    public async Task DispatchAsync_ForVerificationEmailRequested_SendsExpectedEmail()
    {
        var evt = new VerificationEmailRequested(Guid.NewGuid(), "jane@example.com", "Jane", "ABC123");

        await _sut.DispatchAsync(EventNames.VerificationEmailRequested, Serialize(evt));

        _emailSenderMock.Verify(s => s.SendAsync(
            It.Is<EmailMessage>(m => m.ToEmail == "jane@example.com" && m.HtmlBody.Contains("ABC123")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ForOrganizationCreated_SendsToRecipientEmail()
    {
        var evt = new OrganizationCreatedNotification(Guid.NewGuid(), "Acme Events", "kontakt@acme.example");

        await _sut.DispatchAsync(EventNames.OrganizationCreated, Serialize(evt));

        _emailSenderMock.Verify(s => s.SendAsync(
            It.Is<EmailMessage>(m => m.ToEmail == "kontakt@acme.example" && m.HtmlBody.Contains("Acme Events")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ForOrganizationAdminDeleted_SendsToRecipientEmailWithReason()
    {
        var evt = new OrganizationAdminDeletedNotification(
            Guid.NewGuid(), "Acme Events", "John Smith", "kontakt@acme.example", "Kršenje pravila.");

        await _sut.DispatchAsync(EventNames.OrganizationAdminDeleted, Serialize(evt));

        _emailSenderMock.Verify(s => s.SendAsync(
            It.Is<EmailMessage>(m => m.ToEmail == "kontakt@acme.example" && m.HtmlBody.Contains("Kršenje pravila.")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ForPasswordResetRequested_SendsEmailContainingResetLink()
    {
        var evt = new PasswordResetRequested(Guid.NewGuid(), "jane@example.com", "Jane", "raw-token-value");

        await _sut.DispatchAsync(EventNames.PasswordResetRequested, Serialize(evt));

        _emailSenderMock.Verify(s => s.SendAsync(
            It.Is<EmailMessage>(m => m.ToEmail == "jane@example.com" && m.HtmlBody.Contains("raw-token-value")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ForPasswordResetRequested_LoggedOutputNeverContainsRawResetToken()
    {
        var evt = new PasswordResetRequested(Guid.NewGuid(), "jane@example.com", "Jane", "super-secret-raw-token");

        await _sut.DispatchAsync(EventNames.PasswordResetRequested, Serialize(evt));

        _logger.Messages.Should().NotContain(m => m.Contains("super-secret-raw-token"));
    }

    [Fact]
    public async Task DispatchAsync_ForAdminPasswordChanged_SendsExpectedEmail()
    {
        var evt = new AdminPasswordChangedNotification(Guid.NewGuid(), "jane@example.com", "Jane");

        await _sut.DispatchAsync(EventNames.AdminPasswordChanged, Serialize(evt));

        _emailSenderMock.Verify(s => s.SendAsync(
            It.Is<EmailMessage>(m => m.ToEmail == "jane@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_UnknownRoutingKey_ThrowsPoisonMessageException()
    {
        var act = async () => await _sut.DispatchAsync("some.unknown.key", Encoding.UTF8.GetBytes("{}"));

        await act.Should().ThrowAsync<PoisonMessageException>();
        _emailSenderMock.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_MalformedJson_ThrowsPoisonMessageException()
    {
        var act = async () => await _sut.DispatchAsync(EventNames.VerificationEmailRequested, Encoding.UTF8.GetBytes("not valid json"));

        await act.Should().ThrowAsync<PoisonMessageException>();
        _emailSenderMock.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
