using eTicketing.Contracts.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Contracts.Tests.Persistence;

public class AuditableEntitySaveChangesInterceptorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContext _context;

    public AuditableEntitySaveChangesInterceptorTests()
    {
        // Sqlite in-memory (kept alive via an open connection) is more faithful than EF's
        // InMemory provider for constraint behavior — same rationale as the plan calls for.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditableEntitySaveChangesInterceptor())
            .Options;

        _context = new TestDbContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task AddingEntity_SetsCreatedAtAndUpdatedAt()
    {
        var before = DateTime.UtcNow;
        var entity = new TestEntity { Name = "first" };

        _context.Entities.Add(entity);
        await _context.SaveChangesAsync();

        var after = DateTime.UtcNow;

        entity.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        entity.UpdatedAt.Should().Be(entity.CreatedAt);
    }

    [Fact]
    public async Task ModifyingEntity_UpdatesUpdatedAtButLeavesCreatedAtUntouched()
    {
        var entity = new TestEntity { Name = "first" };
        _context.Entities.Add(entity);
        await _context.SaveChangesAsync();

        var originalCreatedAt = entity.CreatedAt;
        await Task.Delay(10);

        entity.Name = "changed";
        await _context.SaveChangesAsync();

        entity.CreatedAt.Should().Be(originalCreatedAt);
        entity.UpdatedAt.Should().BeAfter(originalCreatedAt);
    }

    

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
