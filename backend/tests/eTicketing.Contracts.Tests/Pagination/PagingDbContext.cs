using eTicketing.Contracts.Pagination;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Contracts.Tests.Pagination;

public class PagingDbContext : DbContext
{
    public PagingDbContext(DbContextOptions<PagingDbContext> options) : base(options) { }

    public DbSet<PagedRow> Rows => Set<PagedRow>();
}
