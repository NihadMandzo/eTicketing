using eTicketing.Catalog.Business.Products;
using eTicketing.Catalog.Business.Tests.TestFixtures;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Persistence;
using FluentAssertions;

namespace eTicketing.Catalog.Business.Tests.Products;

/// <summary>
/// GET /internal/products/organization-stats — the catalogue half of eTicketing.Ticketing's
/// Organizacije report. Ticketing knows what each organization sold; only Catalog knows what each
/// one has listed, how much of it is still a draft, and which listings have no photo.
/// </summary>
public class OrganizationProductStatsTests : IDisposable
{
    private readonly CatalogTestContext _fixture = new();
    private readonly IProductService _sut;

    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();
    private Category _category = null!;

    public OrganizationProductStatsTests()
    {
        // Every assertion below picks its own organization out of the result rather than asserting
        // on the whole list: CatalogDbContext seeds demo products via HasData, which EnsureCreated
        // applies, so the catalogue these tests run against is never empty to begin with.
        _sut = _fixture.CreateProductService();

        _category = new Category { Name = "Muzika", IsActive = true, TicketingMode = TicketingMode.SingleOccurrence };
        _fixture.CategoryRepository.AddAsync(_category).GetAwaiter().GetResult();
        _fixture.UnitOfWork.SaveChangesAsync().GetAwaiter().GetResult();
    }

    private async Task SeedProductAsync(Guid organizationId, PublishStatus status, bool withImage)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Proizvod",
            Description = "Opis proizvoda za test.",
            Date = new DateTime(2026, 12, 1, 20, 0, 0, DateTimeKind.Utc),
            CategoryId = _category.Id,
            OrganizationId = organizationId,
            Status = status,
            Latitude = 43.34,
            Longitude = 17.81,
            City = City.Mostar,
        };

        if (withImage)
            product.Images.Add(new ProductImage { Id = Guid.NewGuid(), BlobName = "slika.jpg", DisplayOrder = 0 });

        await _fixture.ProductRepository.AddAsync(product);
        await _fixture.UnitOfWork.SaveChangesAsync();
    }

    [Fact]
    public async Task GetOrganizationStatsAsync_CountsPublishedDraftAndImagelessProductsPerOrganization()
    {
        await SeedProductAsync(_orgA, PublishStatus.Published, withImage: true);
        await SeedProductAsync(_orgA, PublishStatus.Published, withImage: false);
        await SeedProductAsync(_orgA, PublishStatus.Draft, withImage: false);

        var result = await _sut.GetOrganizationStatsAsync();

        result.IsSuccess.Should().BeTrue();
        var stats = result.Value!.Single(s => s.OrganizationId == _orgA);
        stats.Total.Should().Be(3);
        stats.Published.Should().Be(2);
        stats.Draft.Should().Be(1);
        stats.WithoutImage.Should().Be(2);
    }

    [Fact]
    public async Task GetOrganizationStatsAsync_KeepsOrganizationsApart()
    {
        await SeedProductAsync(_orgA, PublishStatus.Published, withImage: true);
        await SeedProductAsync(_orgB, PublishStatus.Draft, withImage: false);
        await SeedProductAsync(_orgB, PublishStatus.Draft, withImage: false);

        var result = await _sut.GetOrganizationStatsAsync();

        result.Value!.Single(s => s.OrganizationId == _orgA).Published.Should().Be(1);
        result.Value.Single(s => s.OrganizationId == _orgA).Draft.Should().Be(0);
        result.Value.Single(s => s.OrganizationId == _orgB).Draft.Should().Be(2);
        result.Value.Single(s => s.OrganizationId == _orgB).Published.Should().Be(0);
    }

    [Fact]
    public async Task GetOrganizationStatsAsync_OmitsAnOrganizationWithNoProducts()
    {
        await SeedProductAsync(_orgA, PublishStatus.Published, withImage: true);

        var result = await _sut.GetOrganizationStatsAsync();

        // An absent row means "this organization has nothing listed" — the Ticketing side never
        // invents a zero row for one it did not get back.
        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().NotContain(s => s.OrganizationId == _orgB);
    }

    public void Dispose() => _fixture.Dispose();
}
