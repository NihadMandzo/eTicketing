using eTicketing.Contracts.Events;
using eTicketing.Notifications.Messaging;
using FluentAssertions;

namespace eTicketing.Notifications.Tests.Messaging;

/// <summary>A retry or dead-letter copy is a new message, so anything the original carried that
/// matters later has to be copied onto it explicitly.</summary>
public class RetryMessagePropertiesTests
{
    [Fact]
    public void Create_KeepsTheMessageIdSoARetriedDeliveryIsStillRecognisedAsTheSameOne()
    {
        // Without it, an email that failed once and was then republished by the outbox would reach
        // DeduplicatingDeliveryHandler as two unrelated messages and be sent twice.
        var properties = RetryMessageProperties.Create(EventNames.PasswordResetRequested, "msg-1", retryCount: 2);

        properties.MessageId.Should().Be("msg-1");
    }

    [Fact]
    public void Create_KeepsTheOriginalRoutingKeyInType()
    {
        var properties = RetryMessageProperties.Create(EventNames.TicketPdfReady, "msg-1", retryCount: 1);

        properties.Type.Should().Be(EventNames.TicketPdfReady);
    }

    [Fact]
    public void Create_ForARetry_RecordsTheAttemptInTheRetryCountHeader()
    {
        var properties = RetryMessageProperties.Create(EventNames.TicketPdfReady, "msg-1", retryCount: 3);

        properties.Headers.Should().ContainKey(RetryMessageProperties.RetryCountHeader)
            .WhoseValue.Should().Be(3);
    }

    [Fact]
    public void Create_ForADeadLetterCopy_CarriesNoRetryHeaderButStillKeepsTheId()
    {
        var properties = RetryMessageProperties.Create(EventNames.TicketPdfReady, "msg-1", retryCount: null);

        properties.Headers.Should().BeNull();
        properties.MessageId.Should().Be("msg-1");
    }

    [Fact]
    public void Create_IsPersistent()
    {
        var properties = RetryMessageProperties.Create(EventNames.TicketPdfReady, messageId: null, retryCount: 1);

        properties.Persistent.Should().BeTrue();
    }
}
