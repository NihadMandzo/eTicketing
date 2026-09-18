using eTicketing.Contracts.Messaging;
using eTicketing.Shared.Messaging.Tests.TestFixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Shared.Messaging.Tests;

/// <summary>
/// The inbox's one promise — each message's handler runs once, and its effects and the record that
/// it ran commit together — against a real database, since the whole point is what does and does not
/// get committed.
/// </summary>
public class TransactionalInboxTests : IDisposable
{
    private const string Consumer = "test.inbound";

    private readonly OutboxTestContext _fixture = new();
    private readonly TransactionalInbox<OutboxTestDbContext> _sut;
    private int _handlerRuns;

    public TransactionalInboxTests()
    {
        _sut = new TransactionalInbox<OutboxTestDbContext>(_fixture.DbContext);
    }

    /// <summary>A handler that does what real ones do: writes a row and saves.</summary>
    private Func<CancellationToken, Task> WritesWidget(string name = "widget") => async ct =>
    {
        _handlerRuns++;
        _fixture.DbContext.Widgets.Add(new Widget { Id = Guid.NewGuid(), Name = name });
        await _fixture.DbContext.SaveChangesAsync(ct);
    };

    /// <summary>Production opens a DI scope — a fresh context — per message. Tests that deliver twice
    /// call this between deliveries so they do not accidentally lean on a shared change tracker.</summary>
    private void NextDelivery() => _fixture.DbContext.ChangeTracker.Clear();

    private Task<int> WidgetCountAsync() => _fixture.DbContext.Widgets.AsNoTracking().CountAsync();

    private Task<List<InboxMessage>> RecordsAsync() => _fixture.DbContext.InboxMessages.AsNoTracking().ToListAsync();

    [Fact]
    public async Task ProcessOnceAsync_ForANewMessage_RunsTheHandlerAndRecordsTheMessage()
    {
        var processed = await _sut.ProcessOnceAsync("msg-1", Consumer, WritesWidget());

        processed.Should().BeTrue();
        _handlerRuns.Should().Be(1);
        (await WidgetCountAsync()).Should().Be(1);

        var record = (await RecordsAsync()).Should().ContainSingle().Subject;
        record.MessageId.Should().Be("msg-1");
        record.Consumer.Should().Be(Consumer);
        record.CreatedAt.Should().NotBe(default, "the cleanup job deletes by it");
    }

    [Fact]
    public async Task ProcessOnceAsync_ForTheSameMessageTwice_RunsTheHandlerOnlyTheFirstTime()
    {
        // The outbox republishing a row whose delete never committed, or the broker redelivering an
        // unacked message: same id, twice.
        await _sut.ProcessOnceAsync("msg-1", Consumer, WritesWidget());
        NextDelivery();

        var repeat = await _sut.ProcessOnceAsync("msg-1", Consumer, WritesWidget());

        repeat.Should().BeFalse();
        _handlerRuns.Should().Be(1);
        (await WidgetCountAsync()).Should().Be(1);
        (await RecordsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessOnceAsync_ForTwoDifferentMessages_RunsTheHandlerForEach()
    {
        // Two edits to the same product are two messages and two sets of notifications, even with
        // identical content — the inbox must key on the message, not on what it says.
        await _sut.ProcessOnceAsync("msg-1", Consumer, WritesWidget());
        NextDelivery();
        await _sut.ProcessOnceAsync("msg-2", Consumer, WritesWidget());

        _handlerRuns.Should().Be(2);
        (await RecordsAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task ProcessOnceAsync_ForTheSameMessageUnderAnotherConsumer_RunsTheHandlerForEach()
    {
        await _sut.ProcessOnceAsync("msg-1", Consumer, WritesWidget());
        NextDelivery();

        var otherConsumer = await _sut.ProcessOnceAsync("msg-1", "other.inbound", WritesWidget());

        otherConsumer.Should().BeTrue();
        _handlerRuns.Should().Be(2);
    }

    [Fact]
    public async Task ProcessOnceAsync_CommitsTheOutboxRowsTheHandlerPublishesTogetherWithTheRecord()
    {
        // The fan-out case: a consumer that turns one message into many outbox rows.
        var publisher = new OutboxEventPublisher<OutboxTestDbContext>(_fixture.DbContext);

        await _sut.ProcessOnceAsync("msg-1", Consumer, async ct =>
        {
            await publisher.PublishAsync("buyer.notified", new { Email = "ana@example.com" }, ct);
            await publisher.PublishAsync("buyer.notified", new { Email = "marko@example.com" }, ct);
            await _fixture.DbContext.SaveChangesAsync(ct);
        });

        (await _fixture.DbContext.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(2);
        (await RecordsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessOnceAsync_WhenTheHandlerThrows_RollsBackItsWritesAndRecordsNothing()
    {
        var process = () => _sut.ProcessOnceAsync("msg-1", Consumer, async ct =>
        {
            await WritesWidget()(ct);
            throw new InvalidOperationException("obrada nije uspjela");
        });

        await process.Should().ThrowAsync<InvalidOperationException>().WithMessage("obrada nije uspjela");
        (await WidgetCountAsync()).Should().Be(0);
        (await RecordsAsync()).Should().BeEmpty("a record would make the retry skip a message that never took effect");
    }

    [Fact]
    public async Task ProcessOnceAsync_WhenTheHandlerSavesTwiceAndThenThrows_KeepsNeitherSave()
    {
        // Each SaveChangesAsync inside the handler joins the inbox's transaction instead of
        // committing on its own — otherwise a half-finished fan-out would stay behind.
        var process = () => _sut.ProcessOnceAsync("msg-1", Consumer, async ct =>
        {
            await WritesWidget("first")(ct);
            await WritesWidget("second")(ct);
            throw new InvalidOperationException("obrada nije uspjela");
        });

        await process.Should().ThrowAsync<InvalidOperationException>();
        (await WidgetCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProcessOnceAsync_AfterAFailedAttempt_RunsTheHandlerAgainOnTheRetry()
    {
        // Deliberately no NextDelivery() between the attempts: the inbox must forget the record it
        // failed to commit, or the same context would refuse to track a second one under that key.
        var failOnce = true;
        var process = () => _sut.ProcessOnceAsync("msg-1", Consumer, async ct =>
        {
            await WritesWidget()(ct);
            if (failOnce)
            {
                failOnce = false;
                throw new InvalidOperationException("prolazna greška");
            }
        });

        await process.Should().ThrowAsync<InvalidOperationException>();
        var retried = await process();

        retried.Should().BeTrue();
        _handlerRuns.Should().Be(2);
        (await WidgetCountAsync()).Should().Be(1);
        (await RecordsAsync()).Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessOnceAsync_WithoutAMessageId_RunsTheHandlerEveryTimeAndRecordsNothing(string? messageId)
    {
        await _sut.ProcessOnceAsync(messageId, Consumer, WritesWidget());
        NextDelivery();
        var second = await _sut.ProcessOnceAsync(messageId, Consumer, WritesWidget());

        second.Should().BeTrue();
        _handlerRuns.Should().Be(2);
        (await RecordsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessOnceAsync_WhenAConcurrentCopyCommittedItsRecordFirst_ReportsADuplicateWithoutRunningTheHandler()
    {
        await using var context = _fixture.NewDbContext(
            new LostInboxRaceInterceptor("msg-1", Consumer, winnerCommits: true));
        var sut = new TransactionalInbox<OutboxTestDbContext>(context);

        var processed = await sut.ProcessOnceAsync("msg-1", Consumer, WritesWidget());

        processed.Should().BeFalse();
        _handlerRuns.Should().Be(0);
        (await RecordsAsync()).Should().ContainSingle("the winning copy's record, and only that one");
    }

    [Fact]
    public async Task ProcessOnceAsync_WhenRecordingFailsForAnotherReason_RethrowsInsteadOfClaimingADuplicate()
    {
        // No record appeared, so this was not a race. Reporting a duplicate here would ack a message
        // that was never processed.
        await using var context = _fixture.NewDbContext(
            new LostInboxRaceInterceptor("msg-1", Consumer, winnerCommits: false));
        var sut = new TransactionalInbox<OutboxTestDbContext>(context);

        var process = () => sut.ProcessOnceAsync("msg-1", Consumer, WritesWidget());

        await process.Should().ThrowAsync<DbUpdateException>();
        _handlerRuns.Should().Be(0);
        (await RecordsAsync()).Should().BeEmpty();
    }

    public void Dispose() => _fixture.Dispose();
}
