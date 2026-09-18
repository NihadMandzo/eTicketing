using System.Text.Json;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Messaging;
using eTicketing.Shared.Messaging.Tests.TestFixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Shared.Messaging.Tests;

/// <summary>
/// The property the whole outbox rests on: a publish is part of the caller's transaction, not a
/// separate act that happens to follow it.
///
/// Worth stating why these are not redundant with the per-service tests. Every service test mocks
/// <see cref="IEventPublisher"/> and asserts it was *called* — which stays green whether the call
/// writes a row, writes nothing, or happens after a save that would never commit it. Only a test
/// against the real publisher can tell those apart.
/// </summary>
public class OutboxEventPublisherTests : IDisposable
{
    private readonly OutboxTestContext _fixture = new();
    private readonly OutboxEventPublisher<OutboxTestDbContext> _sut;

    private record SomethingHappened(Guid Id, string Name);

    public OutboxEventPublisherTests()
    {
        _sut = new OutboxEventPublisher<OutboxTestDbContext>(_fixture.DbContext);
    }

    [Fact]
    public async Task PublishAsync_DoesNotSaveByItself()
    {
        await _sut.PublishAsync("test.happened", new SomethingHappened(Guid.NewGuid(), "A"));

        // Nothing committed yet — the row is only in the change tracker. This is the behaviour that
        // makes the publish join the caller's transaction, and also the one that makes a publish
        // placed *after* SaveChangesAsync silently do nothing at all.
        var committed = await _fixture.DbContext.OutboxMessages.AsNoTracking().CountAsync();
        committed.Should().Be(0);
    }

    [Fact]
    public async Task PublishAsync_ThenCallerSaves_CommitsTheMessage()
    {
        var payload = new SomethingHappened(Guid.NewGuid(), "A");

        await _sut.PublishAsync("test.happened", payload);
        await _fixture.DbContext.SaveChangesAsync();

        var message = await _fixture.DbContext.OutboxMessages.AsNoTracking().SingleAsync();
        message.RoutingKey.Should().Be("test.happened");
        message.Id.Should().NotBeEmpty();
        message.AttemptCount.Should().Be(0);
        message.LastError.Should().BeNull();

        // Serialized, not typed: the table never needs to know the event shapes, so an old row
        // still dispatches after the publisher's type has moved on.
        JsonSerializer.Deserialize<SomethingHappened>(message.Payload).Should().Be(payload);
    }

    [Fact]
    public async Task PublishAsync_CommitsInTheSameTransactionAsTheDataThatCausedIt()
    {
        _fixture.DbContext.Widgets.Add(new Widget { Id = Guid.NewGuid(), Name = "Nešto" });
        await _sut.PublishAsync("test.happened", new SomethingHappened(Guid.NewGuid(), "A"));

        await _fixture.DbContext.SaveChangesAsync();

        (await _fixture.DbContext.Widgets.AsNoTracking().CountAsync()).Should().Be(1);
        (await _fixture.DbContext.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_WhenTheCallersSaveFails_LeavesNoMessage()
    {
        // The failure the outbox exists to survive, seen from the other side: if the write the
        // event describes does not commit, neither may the event. Forced with a duplicate primary
        // key, so the save genuinely fails at the database rather than being simulated.
        var id = Guid.NewGuid();
        _fixture.DbContext.Widgets.Add(new Widget { Id = id, Name = "Prvi" });
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        _fixture.DbContext.Widgets.Add(new Widget { Id = id, Name = "Duplikat" });
        await _sut.PublishAsync("test.happened", new SomethingHappened(id, "A"));

        var save = async () => await _fixture.DbContext.SaveChangesAsync();
        await save.Should().ThrowAsync<DbUpdateException>();

        _fixture.DbContext.ChangeTracker.Clear();
        (await _fixture.DbContext.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PublishAsync_CalledSeveralTimes_WritesOneRowEachWithDistinctIds()
    {
        // The fan-out shape: ProductDeletionNotifier publishes once per recipient before a single
        // save, so several rows have to survive one transaction with ids that do not collide.
        await _sut.PublishAsync("test.happened", new SomethingHappened(Guid.NewGuid(), "A"));
        await _sut.PublishAsync("test.happened", new SomethingHappened(Guid.NewGuid(), "B"));
        await _sut.PublishAsync("test.other", new SomethingHappened(Guid.NewGuid(), "C"));
        await _fixture.DbContext.SaveChangesAsync();

        var messages = await _fixture.DbContext.OutboxMessages.AsNoTracking().ToListAsync();
        messages.Should().HaveCount(3);
        messages.Select(m => m.Id).Should().OnlyHaveUniqueItems();
        messages.Count(m => m.RoutingKey == "test.happened").Should().Be(2);
    }

    public void Dispose() => _fixture.Dispose();
}
