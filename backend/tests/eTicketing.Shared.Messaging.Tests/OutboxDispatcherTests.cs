using eTicketing.Contracts.Messaging;
using eTicketing.Shared.Messaging.Tests.TestFixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace eTicketing.Shared.Messaging.Tests;

/// <summary>
/// The other half of the outbox: getting committed rows to the broker without losing them.
///
/// The dispatcher's <c>ExecuteAsync</c> loop is a timer around one method, so these drive that
/// method directly rather than starting a hosted service and waiting on wall-clock time — the
/// behaviour worth pinning is what a single pass does, not that <see cref="PeriodicTimer"/> works.
/// </summary>
public class OutboxDispatcherTests : IDisposable
{
    private readonly OutboxTestContext _fixture = new();
    private readonly RecordingRawPublisher _publisher = new();
    private readonly Mock<IOutboxDispatchLock> _dispatchLock = new();
    private readonly ManualTimeProvider _clock = new();
    private readonly OutboxDispatcher<OutboxTestDbContext> _sut;

    public OutboxDispatcherTests()
    {
        // Granted by default: every test but the lock ones is about what a pass does once it runs.
        _dispatchLock
            .Setup(l => l.TryAcquireAsync(It.IsAny<DbContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _sut = new OutboxDispatcher<OutboxTestDbContext>(
            _fixture.ScopeFactory, _publisher, _dispatchLock.Object,
            NullLogger<OutboxDispatcher<OutboxTestDbContext>>.Instance, _clock);
    }

    /// <summary>Captures what reached the broker, and can be told to fail — the two things every
    /// test here needs. A Moq setup would do, but published messages are asserted on by order and
    /// content in most cases, which reads better as a list.</summary>
    private sealed class RecordingRawPublisher : IRawEventPublisher
    {
        public List<(string RoutingKey, string Payload, Guid MessageId)> Published { get; } = [];
        public Exception? FailWith { get; set; }

        /// <summary>Routing keys the broker refuses as unroutable — the per-message failure.</summary>
        public HashSet<string> Refuse { get; } = [];

        public Task PublishRawAsync(string routingKey, string payloadJson, Guid messageId, CancellationToken ct = default)
        {
            if (FailWith is not null)
                return Task.FromException(FailWith);

            if (Refuse.Contains(routingKey))
                return Task.FromException(new EventRejectedException(routingKey, new InvalidOperationException("NO_ROUTE")));

            Published.Add((routingKey, payloadJson, messageId));
            return Task.CompletedTask;
        }
    }

    /// <summary>One pass, the same call the timer loop makes.</summary>
    private Task DispatchAsync() => _sut.DispatchPendingAsync(CancellationToken.None);

    private async Task<OutboxMessage> SeedAsync(string routingKey = "test.happened", string payload = "{}")
    {
        var message = new OutboxMessage { Id = Guid.NewGuid(), RoutingKey = routingKey, Payload = payload };
        _fixture.DbContext.OutboxMessages.Add(message);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
        return message;
    }

    [Fact]
    public async Task DispatchPending_PublishesAndThenDeletesTheRow()
    {
        var message = await SeedAsync("ticket.purchased", """{"orderId":"x"}""");

        await DispatchAsync();

        _publisher.Published.Should().ContainSingle();
        _publisher.Published[0].RoutingKey.Should().Be("ticket.purchased");
        _publisher.Published[0].Payload.Should().Be("""{"orderId":"x"}""");

        // Deleted only after the publish returned. The reverse order would lose the event whenever
        // the process died in between; this order can publish twice instead, which is the trade.
        (await _fixture.DbContext.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(0);
        _publisher.Published[0].MessageId.Should().Be(message.Id);
    }

    [Fact]
    public async Task DispatchPending_PublishesUnderTheRowsOwnId()
    {
        // Not a fresh Guid per attempt: a row published twice has to arrive with the same id both
        // times, or a future inbox table cannot recognise the repeat.
        var message = await SeedAsync();

        await DispatchAsync();

        _publisher.Published[0].MessageId.Should().Be(message.Id);
    }

    [Fact]
    public async Task DispatchPending_WhenPublishFails_KeepsTheRowAndRecordsTheAttempt()
    {
        var message = await SeedAsync();
        _publisher.FailWith = new InvalidOperationException("broker je nedostupan");

        await DispatchAsync();

        var stored = await _fixture.DbContext.OutboxMessages.AsNoTracking().SingleAsync();
        stored.Id.Should().Be(message.Id);
        stored.AttemptCount.Should().Be(1);
        stored.LastError.Should().Be("broker je nedostupan");
    }

    [Fact]
    public async Task DispatchPending_AfterAFailedPass_DoesNotRetryBeforeTheBackoff()
    {
        await SeedAsync();
        _publisher.FailWith = new InvalidOperationException("broker je nedostupan");
        await DispatchAsync();

        _publisher.FailWith = null;
        _clock.Advance(TimeSpan.FromSeconds(30));
        await DispatchAsync();

        _publisher.Published.Should().BeEmpty();
        (await _fixture.DbContext.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DispatchPending_AfterAFailedPass_RetriesOnceTheBackoffHasPassed()
    {
        await SeedAsync();
        _publisher.FailWith = new InvalidOperationException("broker je nedostupan");
        await DispatchAsync();

        _publisher.FailWith = null;
        _clock.Advance(TimeSpan.FromMinutes(2));
        await DispatchAsync();

        _publisher.Published.Should().ContainSingle();
        (await _fixture.DbContext.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DispatchPending_WhenTheBrokerRefusesOneMessage_PublishesTheRestOfTheBatch()
    {
        // The regression: one unroutable payment.failed row used to hold 5,370 ticket.purchased
        // events behind it for good.
        await SeedAsync("payment.failed");
        await SeedAsync("ticket.purchased");
        await SeedAsync("ticket.purchased");
        _publisher.Refuse.Add("payment.failed");

        await DispatchAsync();

        _publisher.Published.Select(p => p.RoutingKey).Should().Equal("ticket.purchased", "ticket.purchased");
        var stored = await _fixture.DbContext.OutboxMessages.AsNoTracking().SingleAsync();
        stored.RoutingKey.Should().Be("payment.failed");
        stored.AttemptCount.Should().Be(1);
        stored.LastError.Should().Contain("NO_ROUTE");
    }

    [Fact]
    public async Task DispatchPending_RefusedRow_GoesOutOnceItsQueueExists()
    {
        await SeedAsync("payment.failed");
        _publisher.Refuse.Add("payment.failed");
        await DispatchAsync();

        _publisher.Refuse.Clear();
        _clock.Advance(TimeSpan.FromMinutes(2));
        await DispatchAsync();

        _publisher.Published.Should().ContainSingle(p => p.RoutingKey == "payment.failed");
        (await _fixture.DbContext.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DispatchPending_FreshRowsGoAheadOfRowsDueForRetry()
    {
        // A pile of rows the broker keeps refusing must never crowd new events out of a batch.
        await SeedManyAsync(OutboxDispatcher<OutboxTestDbContext>.BatchSize, "payment.failed");
        _publisher.Refuse.Add("payment.failed");
        await DispatchAsync();

        _clock.Advance(TimeSpan.FromMinutes(2));
        var fresh = await SeedAsync("ticket.purchased");
        await DispatchAsync();

        _publisher.Published.Should().ContainSingle(p => p.MessageId == fresh.Id);
    }

    [Fact]
    public async Task DispatchPending_RowRefusedManyTimes_IsRetriedHourly()
    {
        await SeedAsync("payment.failed");
        await _fixture.DbContext.OutboxMessages.ExecuteUpdateAsync(s => s.SetProperty(m => m.AttemptCount, 25));

        _clock.Advance(TimeSpan.FromMinutes(30));
        await DispatchAsync();
        _publisher.Published.Should().BeEmpty();

        _clock.Advance(TimeSpan.FromMinutes(31));
        await DispatchAsync();
        _publisher.Published.Should().ContainSingle();
    }

    [Fact]
    public async Task DispatchPending_WithABacklogLargerThanABatch_DrainsItInOneTick()
    {
        // One batch per 5-second tick capped every service at about ten events a second.
        const int backlog = OutboxDispatcher<OutboxTestDbContext>.BatchSize * 3 + 7;
        await SeedManyAsync(backlog);

        await DispatchAsync();

        _publisher.Published.Should().HaveCount(backlog);
        (await _fixture.DbContext.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DispatchPending_StopsDrainingAtTheCapPerTick()
    {
        const int cap = OutboxDispatcher<OutboxTestDbContext>.BatchSize * OutboxDispatcher<OutboxTestDbContext>.MaxPassesPerTick;
        await SeedManyAsync(cap + 30);

        await DispatchAsync();

        _publisher.Published.Should().HaveCount(cap);
        (await _fixture.DbContext.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(30);
    }

    [Fact]
    public async Task DispatchPending_WhenTheBrokerFailsMidBacklog_StopsDraining()
    {
        await SeedManyAsync(OutboxDispatcher<OutboxTestDbContext>.BatchSize * 2);
        _publisher.FailWith = new InvalidOperationException("veza prekinuta");

        await DispatchAsync();

        _publisher.Published.Should().BeEmpty();
        (await _fixture.DbContext.OutboxMessages.AsNoTracking().CountAsync(m => m.AttemptCount == 1)).Should().Be(1);
    }

    private async Task SeedManyAsync(int count, string? routingKey = null)
    {
        for (var i = 0; i < count; i++)
            _fixture.DbContext.OutboxMessages.Add(new OutboxMessage { Id = Guid.NewGuid(), RoutingKey = routingKey ?? $"event.{i}", Payload = "{}" });
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task DispatchPending_StopsThePassOnTheFirstFailure()
    {
        // A broker failure is about the broker, not the message. Ploughing through the rest would
        // burn every row's attempt counter on one outage and shuffle delivery order.
        await SeedAsync("first");
        await SeedAsync("second");
        _publisher.FailWith = new InvalidOperationException("nedostupan");

        await DispatchAsync();

        var stored = await _fixture.DbContext.OutboxMessages.AsNoTracking().ToListAsync();
        stored.Should().HaveCount(2);
        stored.Count(m => m.AttemptCount == 1).Should().Be(1);
        stored.Count(m => m.AttemptCount == 0).Should().Be(1);
    }

    [Fact]
    public async Task DispatchPending_PublishesOldestFirst()
    {
        // Events reach the broker in the order the transactions that produced them committed —
        // "the product changed" must not overtake "the product was created".
        var first = await SeedAsync("first");
        await Task.Delay(10);
        var second = await SeedAsync("second");

        await DispatchAsync();

        _publisher.Published.Select(p => p.MessageId).Should().ContainInOrder(first.Id, second.Id);
    }

    [Fact]
    public async Task DispatchPending_WithAnEmptyOutbox_DoesNothing()
    {
        await DispatchAsync();

        _publisher.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task DispatchPending_DrainsEverythingItCanInOnePass()
    {
        for (var i = 0; i < 5; i++)
            await SeedAsync($"event.{i}");

        await DispatchAsync();

        _publisher.Published.Should().HaveCount(5);
        (await _fixture.DbContext.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DispatchPending_WhenAnotherDispatcherHoldsTheLock_SkipsThePassAndLeavesTheRows()
    {
        // Two replicas polling the same database used to both read and both publish every row. The
        // one that loses the lock must not touch the rows at all — not publish, not bump attempts.
        var message = await SeedAsync();
        _dispatchLock
            .Setup(l => l.TryAcquireAsync(It.IsAny<DbContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await DispatchAsync();

        _publisher.Published.Should().BeEmpty();
        var stored = await _fixture.DbContext.OutboxMessages.AsNoTracking().SingleAsync();
        stored.Id.Should().Be(message.Id);
        stored.AttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task DispatchPending_WhenTheLockIsNotGranted_DoesNotReleaseIt()
    {
        // Releasing a lock this dispatcher never held would, for a session-owned SQL Server lock,
        // raise an error on every skipped tick.
        _dispatchLock
            .Setup(l => l.TryAcquireAsync(It.IsAny<DbContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await DispatchAsync();

        _dispatchLock.Verify(l => l.ReleaseAsync(It.IsAny<DbContext>()), Times.Never);
    }

    [Fact]
    public async Task DispatchPending_AfterASuccessfulPass_ReleasesTheLockOnce()
    {
        await SeedAsync();

        await DispatchAsync();

        _dispatchLock.Verify(l => l.ReleaseAsync(It.IsAny<DbContext>()), Times.Once);
    }

    [Fact]
    public async Task DispatchPending_WhenPublishFails_StillReleasesTheLock()
    {
        // A lock kept after a failed pass would stall every other replica until this one's
        // connection died — the broker outage would outlive itself.
        await SeedAsync();
        _publisher.FailWith = new InvalidOperationException("broker je nedostupan");

        await DispatchAsync();

        _dispatchLock.Verify(l => l.ReleaseAsync(It.IsAny<DbContext>()), Times.Once);
    }

    [Fact]
    public async Task DispatchPending_WhenTheDatabaseReadThrows_StillReleasesTheLock()
    {
        // Stands in for a database that fails mid-pass: the read itself throws.
        await _fixture.DbContext.Database.ExecuteSqlRawAsync("DROP TABLE OutboxMessages");

        var pass = DispatchAsync;

        await pass.Should().ThrowAsync<Exception>();
        _dispatchLock.Verify(l => l.ReleaseAsync(It.IsAny<DbContext>()), Times.Once);
    }

    public void Dispose() => _fixture.Dispose();
}
