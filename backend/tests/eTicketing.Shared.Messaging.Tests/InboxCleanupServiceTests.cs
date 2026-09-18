using eTicketing.Contracts.Messaging;
using eTicketing.Shared.Messaging.Tests.TestFixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace eTicketing.Shared.Messaging.Tests;

/// <summary>
/// Driven through the one-pass method at a chosen moment, same as OutboxDispatcherTests, rather than
/// by starting the hosted service and waiting an hour for its timer.
/// </summary>
public class InboxCleanupServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly OutboxTestContext _fixture = new();
    private readonly InboxCleanupService<OutboxTestDbContext> _sut;

    public InboxCleanupServiceTests()
    {
        _sut = new InboxCleanupService<OutboxTestDbContext>(
            _fixture.ScopeFactory, NullLogger<InboxCleanupService<OutboxTestDbContext>>.Instance);
    }

    /// <summary>Records a processed message as of <paramref name="processedAt"/>. The audit interceptor
    /// stamps CreatedAt with the real clock on save, so the timestamp is moved afterwards with a
    /// set-based update that the interceptor does not see.</summary>
    private async Task SeedAsync(string messageId, DateTime processedAt)
    {
        _fixture.DbContext.InboxMessages.Add(new InboxMessage { MessageId = messageId, Consumer = "test.inbound" });
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        await _fixture.DbContext.InboxMessages
            .Where(m => m.MessageId == messageId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatedAt, processedAt));
    }

    private async Task<List<string>> RemainingAsync() =>
        await _fixture.DbContext.InboxMessages.AsNoTracking().Select(m => m.MessageId).ToListAsync();

    [Fact]
    public async Task DeleteExpiredAsync_RemovesRecordsOlderThanTheRetention()
    {
        await SeedAsync("old", Now - InboxCleanupService<OutboxTestDbContext>.Retention - TimeSpan.FromDays(1));

        var deleted = await _sut.DeleteExpiredAsync(Now, CancellationToken.None);

        deleted.Should().Be(1);
        (await RemainingAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteExpiredAsync_KeepsRecordsStillInsideTheRetention()
    {
        // A repeat can still arrive for these, so deleting them would let it be processed again.
        await SeedAsync("recent", Now - TimeSpan.FromDays(1));
        await SeedAsync("at-the-cutoff", Now - InboxCleanupService<OutboxTestDbContext>.Retention);

        var deleted = await _sut.DeleteExpiredAsync(Now, CancellationToken.None);

        deleted.Should().Be(0);
        (await RemainingAsync()).Should().BeEquivalentTo(["recent", "at-the-cutoff"]);
    }

    [Fact]
    public async Task DeleteExpiredAsync_WithAMixOfAges_RemovesOnlyTheExpiredOnes()
    {
        await SeedAsync("old", Now - TimeSpan.FromDays(45));
        await SeedAsync("recent", Now - TimeSpan.FromDays(2));

        await _sut.DeleteExpiredAsync(Now, CancellationToken.None);

        (await RemainingAsync()).Should().Equal("recent");
    }

    public void Dispose() => _fixture.Dispose();
}
