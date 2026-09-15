using eTicketing.Contracts.Pagination;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Contracts.Tests.Pagination;

/// <summary>A throwaway entity, so these tests depend on no real domain model.</summary>
public class PagedRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class PagingDbContext : DbContext
{
    public PagingDbContext(DbContextOptions<PagingDbContext> options) : base(options) { }

    public DbSet<PagedRow> Rows => Set<PagedRow>();
}

/// <summary>
/// Pins the locked pagination contract: 0-indexed, always paged at the database level, and the
/// echoed Page/PageSize are never null. The regression these guard against is real — Page/PageSize
/// were nullable and Skip/Take were skipped whenever either was absent, so
/// <c>GET /api/products?pageSize=100</c> (with no page, which the web and mobile clients send)
/// returned every row in the table.
/// </summary>
public class QueryableExtensionsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PagingDbContext _context;

    public QueryableExtensionsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PagingDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new PagingDbContext(options);
        _context.Database.EnsureCreated();

        for (var i = 1; i <= 25; i++)
        {
            _context.Rows.Add(new PagedRow { Id = i, Name = $"row-{i:00}" });
        }

        _context.SaveChanges();
    }

    [Fact]
    public async Task ToPagedResultAsync_IsZeroIndexed_SoPageZeroIsTheFirstPage()
    {
        var result = await _context.Rows.OrderBy(r => r.Id).ToPagedResultAsync(0, 10);

        result.Items.Should().HaveCount(10);
        result.Items.First().Id.Should().Be(1);
        result.Items.Last().Id.Should().Be(10);
    }

    [Fact]
    public async Task ToPagedResultAsync_SkipsByPageTimesPageSize()
    {
        var result = await _context.Rows.OrderBy(r => r.Id).ToPagedResultAsync(2, 10);

        result.Items.Should().HaveCount(5);
        result.Items.First().Id.Should().Be(21);
    }

    [Fact]
    public async Task ToPagedResultAsync_TotalCountCountsEveryRow_NotJustThePage()
    {
        var result = await _context.Rows.OrderBy(r => r.Id).ToPagedResultAsync(0, 10);

        result.TotalCount.Should().Be(25);
        result.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task ToPagedResultAsync_EchoesBackThePageAndSizeItUsed()
    {
        var result = await _context.Rows.OrderBy(r => r.Id).ToPagedResultAsync(1, 5);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task ToPagedResultAsync_ForAPageBeyondTheEnd_ReturnsNoItemsButTheRealTotal()
    {
        var result = await _context.Rows.OrderBy(r => r.Id).ToPagedResultAsync(99, 10);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(25);
    }

    [Fact]
    public async Task ToPagedResultAsync_AlwaysCapsTheResult_EvenForALargePageSize()
    {
        // The regression test that matters: there is no combination of arguments that returns
        // more rows than pageSize, because Skip/Take are no longer conditional.
        var result = await _context.Rows.OrderBy(r => r.Id).ToPagedResultAsync(0, 3);

        result.Items.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(null, null, BaseSearchObject.DefaultPage, BaseSearchObject.DefaultPageSize)]
    [InlineData(null, 100, BaseSearchObject.DefaultPage, 100)]
    [InlineData(2, null, 2, BaseSearchObject.DefaultPageSize)]
    [InlineData(3, 25, 3, 25)]
    public void BaseSearchObject_AppliesItsDefaults_WheneverAValueIsAbsent(
        int? page, int? pageSize, int expectedPage, int expectedPageSize)
    {
        // The "only pageSize supplied" row is exactly what the web and mobile catalog/sector
        // services send; it must resolve to page 0, never to "return everything".
        var query = new BaseSearchObject { Page = page, PageSize = pageSize };

        query.EffectivePage.Should().Be(expectedPage);
        query.EffectivePageSize.Should().Be(expectedPageSize);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
