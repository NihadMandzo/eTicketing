using eTicketing.Ticketing.Api.Infrastructure.Redis;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace eTicketing.Ticketing.Business.Tests.Infrastructure;

/// <summary>
/// The holdinfo value format, tested directly against a mocked IDatabase.
///
/// This class lives in .Api (infra wiring is a hosting concern) and is mocked away everywhere else,
/// the way the other clients there are — see NarrativeWriterTests for the same exception and why it
/// is made. The reason it applies here: a hold's owner is recorded in this one string, every
/// ownership check in SectorService and PurchaseService is downstream of parsing it, and getting
/// the format wrong fails *open* — every hold silently reads back as unowned and the ownership
/// checks pass for everybody. That is invisible to a test that mocks ISectorCapacityLock.
///
/// The second thing it pins down is the deploy: holds minted by the previous build carry the bare
/// counter key with no version prefix, and they keep arriving for the remaining five minutes of
/// their TTL.
/// </summary>
public class RedisSectorCapacityLockTests
{
    private readonly Mock<IDatabase> _db = new();
    private readonly RedisSectorCapacityLock _sut;

    private static readonly Guid SectorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const string CounterKey = "sector:11111111-1111-1111-1111-111111111111:capacity";
    private const string HoldId = "hold1";

    public RedisSectorCapacityLockTests()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);

        _db.Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)1));

        _sut = new RedisSectorCapacityLock(redis.Object, NullLogger<RedisSectorCapacityLock>.Instance);
    }

    /// <summary>Points holdinfo:hold1 at a raw value, standing in for whatever a previous TryHoldAsync
    /// wrote — including a shape written by an older build.</summary>
    private void GivenHoldInfo(string value) =>
        _db.Setup(d => d.StringGetAsync($"holdinfo:{HoldId}", It.IsAny<CommandFlags>()))
            .ReturnsAsync(value);

    private void GivenHoldField(string counterKey, int quantity) =>
        _db.Setup(d => d.HashGetAsync(counterKey, HoldId, It.IsAny<CommandFlags>()))
            .ReturnsAsync($"{quantity}:{DateTimeOffset.MaxValue.ToUnixTimeSeconds()}");

    /// <summary>The holdinfo value TryHoldAsync actually wrote. Read off the invocation list rather
    /// than through Verify: StringSetAsync has several overloads that differ only in optional
    /// parameters, so a Verify expression has to name the exact one the production call happens to
    /// bind to, and would start passing vacuously if that ever changed.</summary>
    private (string Key, string Value) CapturedHoldInfo()
    {
        var invocation = _db.Invocations.Should().ContainSingle(i => i.Method.Name == "StringSetAsync").Subject;
        return (invocation.Arguments[0].ToString()!, invocation.Arguments[1].ToString()!);
    }

    [Fact]
    public async Task TryHoldAsync_WithAnOwner_WritesTheOwnerIntoTheVersionedHoldInfoValue()
    {
        var hold = await _sut.TryHoldAsync(SectorId, 100, 2, null, TimeSpan.FromMinutes(5), OwnerId);

        var (key, value) = CapturedHoldInfo();
        key.Should().Be($"holdinfo:{hold.HoldId}");
        value.Should().Be($"v2|{OwnerId:N}|{CounterKey}");
    }

    [Fact]
    public async Task TryHoldAsync_WithNoOwner_WritesAnEmptyOwnerSegment()
    {
        // A system hold (TicketPrintService's batch). Still versioned, so it is distinguishable
        // from a pre-upgrade value — both read back as unowned, but only one is deliberate.
        await _sut.TryHoldAsync(SectorId, 100, 2, null, TimeSpan.FromMinutes(5), null);

        CapturedHoldInfo().Value.Should().Be($"v2||{CounterKey}");
    }

    [Fact]
    public async Task TryHoldAsync_ForADailyEntrySector_KeepsTheDateInTheCounterKeyHalf()
    {
        await _sut.TryHoldAsync(SectorId, 100, 1, new DateOnly(2026, 8, 15), TimeSpan.FromMinutes(5), OwnerId);

        CapturedHoldInfo().Value.Should().Be(
            $"v2|{OwnerId:N}|sector:{SectorId}:date:2026-08-15:capacity");
    }

    [Fact]
    public async Task PeekAsync_ForAVersionedValue_ReportsTheOwner()
    {
        GivenHoldInfo($"v2|{OwnerId:N}|{CounterKey}");
        GivenHoldField(CounterKey, 3);

        var reservation = await _sut.PeekAsync(HoldId);

        reservation.Should().NotBeNull();
        reservation!.OwnerId.Should().Be(OwnerId);
        reservation.SectorId.Should().Be(SectorId);
        reservation.Date.Should().BeNull();
        reservation.Quantity.Should().Be(3);
    }

    [Fact]
    public async Task PeekAsync_ForAVersionedValueWithAnEmptyOwnerSegment_ReportsNoOwner()
    {
        GivenHoldInfo($"v2||{CounterKey}");
        GivenHoldField(CounterKey, 1);

        var reservation = await _sut.PeekAsync(HoldId);

        reservation!.OwnerId.Should().BeNull();
        reservation.SectorId.Should().Be(SectorId);
    }

    [Fact]
    public async Task PeekAsync_ForAPreUpgradeValue_ReadsTheWholeValueAsTheCounterKeyAndReportsNoOwner()
    {
        // The deploy case: holds minted by the previous build stored the bare counter key. Reading
        // this as corruption would break every checkout that straddled the deploy.
        GivenHoldInfo(CounterKey);
        GivenHoldField(CounterKey, 2);

        var reservation = await _sut.PeekAsync(HoldId);

        reservation.Should().NotBeNull();
        reservation!.SectorId.Should().Be(SectorId);
        reservation.Quantity.Should().Be(2);
        reservation.OwnerId.Should().BeNull();
    }

    [Fact]
    public async Task PeekAsync_ForADailyEntryPreUpgradeValue_StillResolvesTheDate()
    {
        var dailyCounterKey = $"sector:{SectorId}:date:2026-08-15:capacity";
        GivenHoldInfo(dailyCounterKey);
        GivenHoldField(dailyCounterKey, 1);

        var reservation = await _sut.PeekAsync(HoldId);

        reservation!.Date.Should().Be(new DateOnly(2026, 8, 15));
        reservation.OwnerId.Should().BeNull();
    }

    [Theory]
    [InlineData("v2|")]
    [InlineData("v2||")]
    [InlineData("v2|not-a-guid|sector:11111111-1111-1111-1111-111111111111:capacity")]
    public async Task PeekAsync_ForAMalformedVersionedValue_FailsClosed(string value)
    {
        // Versioned but unreadable is corruption, not an old hold. Resolving it as unowned would
        // reopen exactly the gap the version exists to close, so it resolves as no hold at all.
        GivenHoldInfo(value);
        GivenHoldField(CounterKey, 1);

        (await _sut.PeekAsync(HoldId)).Should().BeNull();
    }

    [Fact]
    public async Task PeekAsync_ForAnUnknownHold_ReturnsNull()
    {
        (await _sut.PeekAsync(HoldId)).Should().BeNull();
    }

    [Fact]
    public async Task ReleaseAsync_ForAVersionedValue_DeletesTheFieldOnTheCounterKey_NotTheRawValue()
    {
        // The regression this pins: if the version prefix were left on the string, every hash
        // operation would address a key named "v2|..." that does not exist, and release/confirm
        // would silently no-op — capacity lost for a full day.
        GivenHoldInfo($"v2|{OwnerId:N}|{CounterKey}");

        await _sut.ReleaseAsync(HoldId);

        _db.Verify(d => d.HashDeleteAsync(CounterKey, HoldId, It.IsAny<CommandFlags>()), Times.Once);
        _db.Verify(d => d.KeyDeleteAsync($"holdinfo:{HoldId}", It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseAsync_ForAPreUpgradeValue_StillDeletesTheFieldOnTheCounterKey()
    {
        GivenHoldInfo(CounterKey);

        await _sut.ReleaseAsync(HoldId);

        _db.Verify(d => d.HashDeleteAsync(CounterKey, HoldId, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_ForAVersionedValue_PersistsTheCounterKeyAndDropsTheHoldInfoPointer()
    {
        GivenHoldInfo($"v2|{OwnerId:N}|{CounterKey}");
        GivenHoldField(CounterKey, 2);

        await _sut.ConfirmAsync(HoldId);

        _db.Verify(
            d => d.HashSetAsync(
                CounterKey, HoldId, It.Is<RedisValue>(v => v.ToString()!.StartsWith("2:")),
                It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Once);
        _db.Verify(d => d.KeyPersistAsync(CounterKey, It.IsAny<CommandFlags>()), Times.Once);
        _db.Verify(d => d.KeyDeleteAsync($"holdinfo:{HoldId}", It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_ForAMalformedVersionedValue_TouchesNothing()
    {
        GivenHoldInfo("v2|not-a-guid|" + CounterKey);

        await _sut.ConfirmAsync(HoldId);

        _db.Verify(
            d => d.HashSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
                It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Never);
        _db.Verify(d => d.KeyPersistAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }
}
