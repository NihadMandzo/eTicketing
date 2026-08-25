using eTicketing.PdfGeneration.Messaging;
using FluentAssertions;

namespace eTicketing.PdfGeneration.Tests.Messaging;

public class RetryQueueNamesTests
{
    [Theory]
    [InlineData(1, "pdfgeneration.tickets.retry.10s")]
    [InlineData(2, "pdfgeneration.tickets.retry.1m")]
    [InlineData(3, "pdfgeneration.tickets.retry.5m")]
    [InlineData(4, "pdfgeneration.tickets.retry.15m")]
    [InlineData(5, "pdfgeneration.tickets.retry.30m")]
    public void ForAttempt_EscalatesThroughTheLadder(int attempt, string expected)
    {
        RetryQueueNames.ForAttempt(attempt).Should().Be(expected);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(50)]
    [InlineData(1000)]
    public void ForAttempt_PastTheLastTier_StaysOnTheSteadyStateQueue(int attempt)
    {
        // Retries never stop, they just stop getting further apart — that's what makes "the PDF is
        // generated the moment Azure is reachable again" true for an outage of any length.
        RetryQueueNames.ForAttempt(attempt).Should().Be("pdfgeneration.tickets.retry.30m");
    }

    [Fact]
    public void Tiers_AreDeclaredInAscendingTtlOrder()
    {
        var ttls = RetryQueueNames.Tiers.Select(t => t.TtlMilliseconds).ToList();

        ttls.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Tiers_CoverEveryQueueForAttemptCanReturn()
    {
        // A tier ForAttempt hands back but RabbitMqTopology never declares would silently drop
        // messages into a queue that doesn't exist.
        var declared = RetryQueueNames.Tiers.Select(t => t.QueueName).ToHashSet();
        var reachable = Enumerable.Range(1, 10).Select(RetryQueueNames.ForAttempt).ToHashSet();

        reachable.Should().BeSubsetOf(declared);
    }

    [Fact]
    public void QueueNames_AreNamespacedToThisService_SoTheyNeverShareNotificationsBacklog()
    {
        RetryQueueNames.Main.Should().StartWith("pdfgeneration.");
        RetryQueueNames.DeadLetter.Should().StartWith("pdfgeneration.");
        RetryQueueNames.Tiers.Should().OnlyContain(t => t.QueueName.StartsWith("pdfgeneration."));
    }
}
