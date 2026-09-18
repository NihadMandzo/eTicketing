using eTicketing.Notifications.Infrastructure.Redis;
using eTicketing.Notifications.Messaging;
using FluentAssertions;
using Moq;
using StackExchange.Redis;

namespace eTicketing.Notifications.Tests.Infrastructure;

/// <summary>
/// The Redis commands themselves, against a mocked IDatabase — same approach as
/// RedisSectorCapacityLockTests in Ticketing. Two things are worth pinning: that claiming is a single
/// scripted step rather than a read followed by a write (a check-then-set would let two replicas both
/// send the same email), and that a mark expires, since a key written without a TTL would grow the
/// set by one entry per email for ever.
/// </summary>
public class RedisProcessedMessageStoreTests
{
    private const string Key = "message:abc";
    private const string NamespacedKey = "notifications:processed:message:abc";

    private readonly Mock<IDatabase> _db = new();
    private readonly RedisProcessedMessageStore _sut;

    public RedisProcessedMessageStoreTests()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);

        _sut = new RedisProcessedMessageStore(redis.Object);
    }

    /// <summary>Whatever the claim script found the key holding afterwards.</summary>
    private void GivenScriptReturns(string value) =>
        _db.Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)value));

    private (string Script, RedisKey[] Keys, RedisValue[] Values) CapturedScript()
    {
        var invocation = _db.Invocations.Should().ContainSingle(i => i.Method.Name == "ScriptEvaluateAsync").Subject;
        return ((string)invocation.Arguments[0]!, (RedisKey[])invocation.Arguments[1]!, (RedisValue[])invocation.Arguments[2]!);
    }

    [Fact]
    public async Task TryClaimAsync_WhenTheKeyWasFree_ReportsTheClaimAsWon()
    {
        GivenScriptReturns(RedisProcessedMessageStore.ProcessingValue);

        var claim = await _sut.TryClaimAsync(Key, TimeSpan.FromMinutes(5));

        claim.Should().Be(DeliveryClaim.Claimed);
    }

    [Fact]
    public async Task TryClaimAsync_WhenTheEmailWasAlreadySent_ReportsItAsProcessed()
    {
        GivenScriptReturns(RedisProcessedMessageStore.ProcessedValue);

        var claim = await _sut.TryClaimAsync(Key, TimeSpan.FromMinutes(5));

        claim.Should().Be(DeliveryClaim.AlreadyProcessed);
    }

    [Fact]
    public async Task TryClaimAsync_ForAKeyWrittenByTheBuildBeforeClaimsExisted_ReportsItAsProcessed()
    {
        // That build wrote a bare "1". Treating it as "someone is sending" would hold the email up
        // for a lease, and treating it as free would send it again — it means the email went out.
        GivenScriptReturns("1");

        var claim = await _sut.TryClaimAsync(Key, TimeSpan.FromMinutes(5));

        claim.Should().Be(DeliveryClaim.AlreadyProcessed);
    }

    [Fact]
    public async Task TryClaimAsync_ClaimsInOneScriptedStepCarryingTheKeyValueAndLease()
    {
        GivenScriptReturns(RedisProcessedMessageStore.ProcessingValue);

        await _sut.TryClaimAsync(Key, TimeSpan.FromMinutes(5));

        var (script, keys, values) = CapturedScript();
        script.Should().Contain("SET").And.Contain("GET");
        keys.Single().ToString().Should().Be(NamespacedKey);
        values[0].ToString().Should().Be(RedisProcessedMessageStore.ProcessingValue);
        values[1].ToString().Should().Be("300", "the lease is passed to Redis in seconds");
    }

    [Fact]
    public async Task MarkProcessedAsync_WritesTheProcessedValueWithTheRetentionAsItsExpiry()
    {
        await _sut.MarkProcessedAsync(Key);

        // Read off the invocation list rather than through Verify, for the same reason as in
        // RedisSectorCapacityLockTests: StringSetAsync's overloads differ only in optional parameters.
        var invocation = _db.Invocations.Should().ContainSingle(i => i.Method.Name == "StringSetAsync").Subject;
        invocation.Arguments[0].ToString().Should().Be(NamespacedKey);
        invocation.Arguments[1].ToString().Should().Be(RedisProcessedMessageStore.ProcessedValue);
        invocation.Arguments.Should().Contain(argument => IsRetention(argument));
    }

    [Fact]
    public async Task ReleaseClaimAsync_DeletesOnlyAClaimThisCallerStillHolds()
    {
        await _sut.ReleaseClaimAsync(Key);

        var (script, keys, values) = CapturedScript();
        script.Should().Contain("DEL");
        keys.Single().ToString().Should().Be(NamespacedKey);
        values.Single().ToString().Should().Be(
            RedisProcessedMessageStore.ProcessingValue,
            "a key that has since been marked processed must survive the release");
    }

    private static bool IsRetention(object? argument) => argument switch
    {
        TimeSpan span => span == RedisProcessedMessageStore.Retention,
        _ => argument?.ToString() == RedisProcessedMessageStore.Retention.ToString(),
    };
}
