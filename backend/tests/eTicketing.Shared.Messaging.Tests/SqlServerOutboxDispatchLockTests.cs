using eTicketing.Shared.Messaging.Tests.TestFixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Shared.Messaging.Tests;

/// <summary>
/// The SQL Server path itself — <c>sp_getapplock</c> on a real server — cannot run on Sqlite and is
/// covered by the manual test plan (two replicas against one database). What is pinned here is the
/// fallback every test project depends on: a non-SQL Server provider is always granted, and neither
/// call touches the connection, which for a shared in-memory Sqlite database would destroy it.
/// </summary>
public class SqlServerOutboxDispatchLockTests : IDisposable
{
    private readonly OutboxTestContext _fixture = new();
    private readonly SqlServerOutboxDispatchLock _sut = new();

    [Fact]
    public async Task TryAcquireAsync_OnANonSqlServerProvider_IsAlwaysGranted()
    {
        var first = await _sut.TryAcquireAsync(_fixture.DbContext, CancellationToken.None);
        var second = await _sut.TryAcquireAsync(_fixture.DbContext, CancellationToken.None);

        first.Should().BeTrue();
        second.Should().BeTrue();
    }

    [Fact]
    public async Task ReleaseAsync_OnANonSqlServerProvider_LeavesTheConnectionUsable()
    {
        await _sut.TryAcquireAsync(_fixture.DbContext, CancellationToken.None);

        await _sut.ReleaseAsync(_fixture.DbContext);

        var read = () => _fixture.DbContext.OutboxMessages.AsNoTracking().CountAsync();
        await read.Should().NotThrowAsync();
    }

    public void Dispose() => _fixture.Dispose();
}
