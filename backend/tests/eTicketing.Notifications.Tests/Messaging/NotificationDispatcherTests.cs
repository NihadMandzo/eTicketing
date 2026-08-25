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

    /// <summary>Stand-in for the bytes PdfGeneration rendered — distinct per ticket so a test can
    /// prove each attachment carries its OWN file, not just the right count.</summary>
    private static byte[] PdfBytesFor(Guid ticketId) => [.. "%PDF-1.4 "u8, .. ticketId.ToByteArray()];

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
    public async Task DispatchAsync_ForTicketPdfReady_SendsOneEmailWithOneAttachmentPerTicket()
    {
        var evt = TicketPdfReadyEvent(2);

        await _sut.DispatchAsync(EventNames.TicketPdfReady, Serialize(evt));

        _emailSenderMock.Verify(s => s.SendAsync(
            It.Is<EmailMessage>(m =>
                m.ToEmail == "buyer@example.com"
                && m.HtmlBody.Contains("Ljetni Festival")
                && m.Attachments.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ForTicketPdfReady_AttachesEachBlobsBytesUnderItsOwnFileName()
    {
        // Guards the 2026-08-24 fix: attachments must carry the PDF bytes, not a URL for Brevo to
        // fetch. The URL form was accepted by Brevo and reported delivered, but never reached
        // recipients.
        var evt = TicketPdfReadyEvent(2);

        await _sut.DispatchAsync(EventNames.TicketPdfReady, Serialize(evt));

        _emailSenderMock.Verify(s => s.SendAsync(
            It.Is<EmailMessage>(m =>
                m.Attachments.Select(a => a.Name).SequenceEqual(evt.Tickets.Select(t => t.FileName))
                && m.Attachments.Select(a => Convert.ToBase64String(a.Content))
                    .SequenceEqual(evt.Tickets.Select(t => Convert.ToBase64String(PdfBytesFor(t.TicketId))))),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ForTicketPdfReady_SendsRealPdfBytesNotAUrl()
    {
        var evt = TicketPdfReadyEvent(1);
        // Hoisted out of the expression tree below — a u8 literal is a ReadOnlySpan, which can't
        // appear inside one.
        var pdfMagic = "%PDF-"u8.ToArray();

        await _sut.DispatchAsync(EventNames.TicketPdfReady, Serialize(evt));

        _emailSenderMock.Verify(s => s.SendAsync(
            It.Is<EmailMessage>(m => m.Attachments.Single().Content.Take(5).SequenceEqual(pdfMagic)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ForProductChanged_SendsToTheAlreadyResolvedRecipient()
    {
        // Ticketing already fanned this out per buyer — Notifications does no lookup of its own.
        var evt = new ProductChangedNotification(Guid.NewGuid(), "Ljetni Festival", "ana@example.com",
            [new ProductFieldChange("Datum i vrijeme", "01.09.2026. 20:00", "02.09.2026. 20:00")]);

        await _sut.DispatchAsync(EventNames.ProductChanged, Serialize(evt));

        _emailSenderMock.Verify(s => s.SendAsync(
            It.Is<EmailMessage>(m =>
                m.ToEmail == "ana@example.com"
                && m.HtmlBody.Contains("02.09.2026. 20:00")
                && m.Attachments.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static TicketPdfReady TicketPdfReadyEvent(int ticketCount)
    {
        var orderId = Guid.NewGuid();
        var tickets = Enumerable.Range(0, ticketCount).Select(i =>
        {
            var ticketId = Guid.NewGuid();
            return new TicketPdf(
                ticketId,
                PdfBytesFor(ticketId),
                $"ulaznica-{i}.pdf",
                "VIP", i == 0 ? "Odrasli" : "Djeca", 50);
        }).ToList();

        return new TicketPdfReady(
            orderId, Guid.NewGuid(), Guid.NewGuid(), "buyer@example.com",
            "Ljetni Festival", new DateTime(2026, 9, 1, 20, 0, 0, DateTimeKind.Utc), "Sarajevo",
            50 * ticketCount, tickets);
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
