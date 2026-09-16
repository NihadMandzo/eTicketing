using eTicketing.Contracts.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Contracts.Tests.Persistence;

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    public DbSet<TestEntity> Entities => Set<TestEntity>();
}
