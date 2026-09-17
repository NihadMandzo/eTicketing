using eTicketing.Notifications.Infrastructure.Redis;
using FluentAssertions;
using Moq;
using StackExchange.Redis;

namespace eTicketing.Notifications.Tests.Infrastructure;

/// <summary>
/// The Redis commands themselves, against a mocked IDatabase — same approach as
/// RedisSectorCapacityLockTests in Ticketing. What is worth pinning is that a mark expires: a key
/// written without a TTL would grow the set by one entry per email for ever.
/// </summary>
public class RedisProcessedMessageStoreTests
{
    private readonly Mock<IDatabase> _db = new();
    private readonly RedisProcessedMessageStore _sut;

    public RedisProcessedMessageStoreTests()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);

        _sut = new RedisProcessedMessageStore(redis.Object);
    }

    [Fact]
    public async Task IsProcessedAsync_ChecksTheNamespacedKey()
    {
        _db.Setup(d => d.KeyExistsAsync("notifications:processed:message:abc", It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var processed = await _sut.IsProcessedAsync("message:abc");

        processed.Should().BeTrue();
    }

    [Fact]
    public async Task IsProcessedAsync_ForAnUnknownKey_ReturnsFalse()
    {
        _db.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(false);

        var processed = await _sut.IsProcessedAsync("message:abc");

        processed.Should().BeFalse();
    }

    [Fact]
    public async Task MarkProcessedAsync_WritesTheNamespacedKeyWithTheRetentionAsItsExpiry()
    {
        await _sut.MarkProcessedAsync("message:abc");

        // Read off the invocation list rather than through Verify, for the same reason as in
        // RedisSectorCapacityLockTests: StringSetAsync's overloads differ only in optional parameters.
        var invocation = _db.Invocations.Should().ContainSingle(i => i.Method.Name == "StringSetAsync").Subject;
        invocation.Arguments[0].ToString().Should().Be("notifications:processed:message:abc");
        invocation.Arguments.Should().Contain(argument => IsRetention(argument));
    }

    private static bool IsRetention(object? argument) => argument switch
    {
        TimeSpan span => span == RedisProcessedMessageStore.Retention,
        _ => argument?.ToString() == RedisProcessedMessageStore.Retention.ToString(),
    };
}
