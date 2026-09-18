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

/// <summary>
/// One email per event, however many times the event arrives. The dispatcher is real and only the
/// email sender and the store are faked, so each test sees the actual route from a delivery to a
/// send — including the dispatcher's own poison handling.
/// </summary>
public class DeduplicatingDeliveryHandlerTests
{
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IProcessedMessageStore> _store = new();
    private readonly DeduplicatingDeliveryHandler _sut;

    /// <summary>What each key holds, so a test can deliver the same event twice and watch the second
    /// one be recognised rather than scripting the answer it expects. Mirrors the Redis key: absent,
    /// claimed, or processed.</summary>
    private readonly Dictionary<string, string> _keys = [];

    public DeduplicatingDeliveryHandlerTests()
    {
        _store.Setup(s => s.TryClaimAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, TimeSpan _, CancellationToken _) =>
            {
                if (_keys.TryGetValue(key, out var held))
                    return held == "processing" ? DeliveryClaim.InProgress : DeliveryClaim.AlreadyProcessed;

                _keys[key] = "processing";
                return DeliveryClaim.Claimed;
            });

        _store.Setup(s => s.MarkProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string key, CancellationToken _) => _keys[key] = "done")
            .Returns(Task.CompletedTask);

        _store.Setup(s => s.ReleaseClaimAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string key, CancellationToken _) =>
            {
                if (_keys.TryGetValue(key, out var held) && held == "processing")
                    _keys.Remove(key);
            })
            .Returns(Task.CompletedTask);

        var dispatcher = new NotificationDispatcher(
            _emailSender.Object,
            MsOptions.Create(new FrontendOptions { WebBaseUrl = "http://localhost:4200" }),
            new ListLogger<NotificationDispatcher>());

        _sut = new DeduplicatingDeliveryHandler(dispatcher, _store.Object, new ListLogger<DeduplicatingDeliveryHandler>());
    }

    private static ReadOnlyMemory<byte> VerificationBody() => JsonSerializer.SerializeToUtf8Bytes(
        new VerificationEmailRequested(Guid.NewGuid(), "jane@example.com", "Jane", "ABC123"));

    private static ReadOnlyMemory<byte> TicketPdfReadyBody(Guid orderId) => JsonSerializer.SerializeToUtf8Bytes(
        new TicketPdfReady(
            orderId, Guid.NewGuid(), Guid.NewGuid(), "buyer@example.com", "Koncert", null, "Sarajevo", 50m,
            [new TicketPdf(Guid.NewGuid(), [1, 2, 3], "ulaznica.pdf", "VIP", null, 50m)]));

    private void VerifySent(Times times) =>
        _emailSender.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task HandleAsync_ForANewMessage_SendsTheEmailAndMarksItProcessed()
    {
        var handled = await _sut.HandleAsync(EventNames.VerificationEmailRequested, "msg-1", VerificationBody());

        handled.Should().BeTrue();
        VerifySent(Times.Once());
        _keys.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new KeyValuePair<string, string>("message:msg-1", "done"));
    }

    [Fact]
    public async Task HandleAsync_ForTheSameMessageDeliveredTwice_SendsTheEmailOnce()
    {
        // The outbox republishing a row whose delete never committed: same id, twice.
        var body = VerificationBody();

        var first = await _sut.HandleAsync(EventNames.VerificationEmailRequested, "msg-1", body);
        var repeat = await _sut.HandleAsync(EventNames.VerificationEmailRequested, "msg-1", body);

        first.Should().BeTrue();
        repeat.Should().BeFalse();
        VerifySent(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_ForTwoDifferentMessages_SendsBoth()
    {
        await _sut.HandleAsync(EventNames.VerificationEmailRequested, "msg-1", VerificationBody());
        await _sut.HandleAsync(EventNames.VerificationEmailRequested, "msg-2", VerificationBody());

        VerifySent(Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_ForARepublishedTicketPdfReadyUnderANewId_SendsTheConfirmationOnce()
    {
        var orderId = Guid.NewGuid();

        await _sut.HandleAsync(EventNames.TicketPdfReady, "pdf-run-1", TicketPdfReadyBody(orderId));
        var repeat = await _sut.HandleAsync(EventNames.TicketPdfReady, "pdf-run-2", TicketPdfReadyBody(orderId));

        repeat.Should().BeFalse();
        VerifySent(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_ClaimsTheKeyBeforeSendingAndMarksItAfterwards()
    {
        // The order is the whole guarantee: claiming after the send would let a second replica send
        // alongside this one, and marking before it would lose the email if the send failed.
        var order = new List<string>();
        _store.Setup(s => s.TryClaimAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("claim"))
            .ReturnsAsync(DeliveryClaim.Claimed);
        _emailSender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("send"))
            .Returns(Task.CompletedTask);
        _store.Setup(s => s.MarkProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("mark"))
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(EventNames.VerificationEmailRequested, "msg-1", VerificationBody());

        order.Should().Equal("claim", "send", "mark");
    }

    [Fact]
    public async Task HandleAsync_WhileAnotherConsumerIsSendingTheSameMessage_AsksToBeRetriedInsteadOfSending()
    {
        // Two replicas handed copies of one message. Skipping here would gamble that the other one's
        // send succeeds; if it fails, nobody would ever send the email.
        _keys["message:msg-1"] = "processing";

        var handle = () => _sut.HandleAsync(EventNames.VerificationEmailRequested, "msg-1", VerificationBody());

        await handle.Should().ThrowAsync<DeliveryInProgressException>();
        VerifySent(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTheSendFails_ReleasesTheClaimSoTheRetryCanSendIt()
    {
        _emailSender.SetupSequence(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Brevo nedostupan"))
            .Returns(Task.CompletedTask);

        var handle = () => _sut.HandleAsync(EventNames.VerificationEmailRequested, "msg-1", VerificationBody());

        await handle.Should().ThrowAsync<HttpRequestException>();
        _keys.Should().BeEmpty("a claim left behind would stall the retry until its lease expired");

        var retried = await handle();

        retried.Should().BeTrue();
        VerifySent(Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_ForAPoisonMessage_PropagatesThePoisonExceptionAndMarksNothing()
    {
        // The consumer's dead-letter routing depends on seeing PoisonMessageException unchanged.
        var handle = () => _sut.HandleAsync("nepoznat.dogadjaj", "msg-1", "{}"u8.ToArray());

        await handle.Should().ThrowAsync<PoisonMessageException>();
        _keys.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenTheClaimCannotBeTaken_SendsTheEmailAnyway()
    {
        // Fails open: a Redis outage must not block verification codes and purchase confirmations.
        _store.Setup(s => s.TryClaimAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Redis nedostupan"));

        var handled = await _sut.HandleAsync(EventNames.VerificationEmailRequested, "msg-1", VerificationBody());

        handled.Should().BeTrue();
        VerifySent(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenMarkingFailsAfterASuccessfulSend_DoesNotThrow()
    {
        // Rethrowing here would send the delivery down the retry ladder and mail it a second time.
        _store.Setup(s => s.MarkProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Redis nedostupan"));

        var handle = () => _sut.HandleAsync(EventNames.VerificationEmailRequested, "msg-1", VerificationBody());

        await handle.Should().NotThrowAsync();
        VerifySent(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenReleasingTheClaimFails_StillReportsTheOriginalSendFailure()
    {
        // The send's exception is what the consumer routes on; a failed release must not replace it.
        _emailSender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Brevo nedostupan"));
        _store.Setup(s => s.ReleaseClaimAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Redis nedostupan"));

        var handle = () => _sut.HandleAsync(EventNames.VerificationEmailRequested, "msg-1", VerificationBody());

        await handle.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task HandleAsync_WithNoMessageId_SendsWithoutTouchingTheStore()
    {
        await _sut.HandleAsync(EventNames.VerificationEmailRequested, messageId: null, VerificationBody());

        VerifySent(Times.Once());
        _store.Verify(
            s => s.TryClaimAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(s => s.MarkProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
