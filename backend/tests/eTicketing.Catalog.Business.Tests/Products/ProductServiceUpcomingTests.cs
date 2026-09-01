using eTicketing.Catalog.Business.Products;
using eTicketing.Catalog.Business.Tests.TestFixtures;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Persistence;
using FluentAssertions;

namespace eTicketing.Catalog.Business.Tests.Products;

/// <summary>
/// GetUpcomingAsync — backs the internal GET /internal/products/upcoming that eTicketing.Ticketing's
/// Dashboard "Nadolazeći događaji" card calls. Covers the filter/sort/cap rules the repository query
/// implements: published only, SingleOccurrence only, future Date only, soonest first, optional
/// organization scoping, and the count cap.
///
/// Every test that doesn't specifically exercise the platform-wide (organizationId: null) path
/// scopes to a freshly-generated organization id, deliberately distinct from
/// ProductSeeder.GetSeedData()'s fixed organizations/products (also SingleOccurrence and Published,
/// with future dates) — those seed rows are present in this Sqlite in-memory database too (applied
/// via EF's HasData on Database.EnsureCreated()), so an org-scoped test never sees them, and the one
/// platform-wide test below asserts by "contains what I seeded" rather than an exact count.
/// </summary>
public class ProductServiceUpcomingTests : IDisposable
{
    private readonly CatalogTestContext _fixture = new();
    private readonly IProductService _sut;

    private readonly int _singleOccurrenceCategory;
    private readonly int _dailyEntryCategory;
    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();

    public ProductServiceUpcomingTests()
    {
        _sut = _fixture.CreateProductService();

        _singleOccurrenceCategory = SeedCategory(TicketingMode.SingleOccurrence);
        _dailyEntryCategory = SeedCategory(TicketingMode.DailyEntry);
    }

    private int SeedCategory(TicketingMode mode)
    {
        var category = new Category { Name = $"Kategorija-{Guid.NewGuid()}", TicketingMode = mode };
        _fixture.DbContext.Set<Category>().Add(category);
        _fixture.DbContext.SaveChanges();
        return category.Id;
    }

    private Product SeedProduct(
        Guid organizationId, DateTime? date, PublishStatus status = PublishStatus.Published, int? categoryId = null)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = $"Proizvod-{Guid.NewGuid()}",
            CategoryId = categoryId ?? _singleOccurrenceCategory,
            OrganizationId = organizationId,
            Status = status,
            Date = date,
            City = City.Mostar,
        };
        _fixture.DbContext.Set<Product>().Add(product);
        _fixture.DbContext.SaveChanges();
        return product;
    }

    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetUpcomingAsync_ReturnsPublishedSingleOccurrenceProductsWithAFutureDate()
    {
        var expected = SeedProduct(_orgA, Now.AddDays(5));

        var result = await _sut.GetUpcomingAsync(organizationId: _orgA, count: 10);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle(p => p.Id == expected.Id);
    }

    [Fact]
    public async Task GetUpcomingAsync_ExcludesDraftProducts()
    {
        SeedProduct(_orgA, Now.AddDays(5), status: PublishStatus.Draft);

        var result = await _sut.GetUpcomingAsync(organizationId: _orgA, count: 10);

        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingAsync_ExcludesPastDates()
    {
        SeedProduct(_orgA, Now.AddDays(-1));

        var result = await _sut.GetUpcomingAsync(organizationId: _orgA, count: 10);

        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingAsync_ExcludesNonSingleOccurrenceProducts()
    {
        // DailyEntry products carry no single Date (null), so they can never satisfy "Date >= now"
        // even if one were seeded with a Date — the category check is what actually excludes them.
        SeedProduct(_orgA, null, categoryId: _dailyEntryCategory);

        var result = await _sut.GetUpcomingAsync(organizationId: _orgA, count: 10);

        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingAsync_WithAnOrganizationId_ReturnsOnlyThatOrganizationsProducts()
    {
        SeedProduct(_orgA, Now.AddDays(3));
        var orgBProduct = SeedProduct(_orgB, Now.AddDays(3));

        var result = await _sut.GetUpcomingAsync(organizationId: _orgB, count: 10);

        result.Value!.Should().ContainSingle(p => p.Id == orgBProduct.Id);
    }

    [Fact]
    public async Task GetUpcomingAsync_WithNoOrganizationId_IncludesProductsFromEveryOrganization()
    {
        // Platform-wide (organizationId: null) also sees ProductSeeder's fixed demo rows, so this
        // asserts "contains both of mine" rather than an exact total count.
        var orgAProduct = SeedProduct(_orgA, Now.AddDays(3));
        var orgBProduct = SeedProduct(_orgB, Now.AddDays(4));

        var result = await _sut.GetUpcomingAsync(organizationId: null, count: 10);

        result.Value!.Select(p => p.Id).Should().Contain([orgAProduct.Id, orgBProduct.Id]);
    }

    [Fact]
    public async Task GetUpcomingAsync_SortsBySoonestDateFirst()
    {
        var later = SeedProduct(_orgA, Now.AddDays(20));
        var soonest = SeedProduct(_orgA, Now.AddDays(2));
        var middle = SeedProduct(_orgA, Now.AddDays(10));

        var result = await _sut.GetUpcomingAsync(organizationId: _orgA, count: 10);

        result.Value!.Select(p => p.Id).Should().ContainInOrder(soonest.Id, middle.Id, later.Id);
    }

    [Fact]
    public async Task GetUpcomingAsync_CapsAtCount()
    {
        for (var i = 0; i < 5; i++)
            SeedProduct(_orgA, Now.AddDays(i + 1));

        var result = await _sut.GetUpcomingAsync(organizationId: _orgA, count: 2);

        result.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUpcomingAsync_WhenNothingIsUpcomingForThatOrganization_ReturnsAnEmptyListRatherThanFailing()
    {
        var result = await _sut.GetUpcomingAsync(organizationId: _orgA, count: 4);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    // PR #26 review fix: `count` reaches this internal endpoint straight from a bare query-string
    // int on Ticketing's Gateway-reachable /reports/upcoming-events, with no request-DTO validator
    // in between. A negative value used to fall straight into ProductRepository.GetUpcomingAsync's
    // .Take(count), which SQL Server rejects at execution — an unhandled 500 for plain bad input.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetUpcomingAsync_WithZeroOrNegativeCount_ReturnsValidationFailure(int count)
    {
        var result = await _sut.GetUpcomingAsync(organizationId: _orgA, count: count);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("product.invalid_count");
    }

    [Fact]
    public async Task GetUpcomingAsync_WithCountAboveMax_ReturnsValidationFailure()
    {
        var result = await _sut.GetUpcomingAsync(organizationId: _orgA, count: 51);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("product.invalid_count");
    }

    public void Dispose() => _fixture.Dispose();
}
