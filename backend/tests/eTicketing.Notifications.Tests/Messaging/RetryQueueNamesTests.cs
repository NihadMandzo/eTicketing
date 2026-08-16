using eTicketing.Notifications.Messaging;
using FluentAssertions;

namespace eTicketing.Notifications.Tests.Messaging;

public class RetryQueueNamesTests
{
    [Theory]
    [InlineData(1, "notifications.email.retry.10s")]
    [InlineData(2, "notifications.email.retry.1m")]
    [InlineData(3, "notifications.email.retry.5m")]
    [InlineData(4, "notifications.email.retry.15m")]
    public void ForAttempt_EscalatesThroughTheLadder(int attempt, string expectedQueue)
    {
        RetryQueueNames.ForAttempt(attempt).Should().Be(expectedQueue);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(50)]
    public void ForAttempt_AtOrBeyondTierFive_StaysOnSteadyStateQueue(int attempt)
    {
        // The concrete assertion behind "an email is never lost, retries never stop" — there is
        // no attempt number that falls off the ladder.
        RetryQueueNames.ForAttempt(attempt).Should().Be("notifications.email.retry.30m");
    }

    [Fact]
    public void Tiers_AreOrderedByIncreasingTtl()
    {
        var ttls = RetryQueueNames.Tiers.Select(t => t.TtlMilliseconds).ToList();

        ttls.Should().BeInAscendingOrder();
        ttls.Should().HaveCount(5);
    }
}
