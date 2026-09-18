using eTicketing.Catalog.Business.Tests.TestFixtures;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Catalog.Business.Tests.Products;

/// <summary>
/// GetByIdWithCategoryAsync and GetByIdWithCategoryNoTrackingAsync are two methods on purpose, and
/// the difference between them is invisible in their results — only in what the change tracker
/// holds afterwards. These pin that, so a later "why are these duplicated, let me merge them"
/// fails here rather than in production.
///
/// The consequence of merging the wrong way is already covered from the other side by
/// ProductServiceTests.UploadImageAsync_ForOwnProduct_StoresBlobAndReturnsUrl: that path relies on
/// EF relationship fix-up appending the new ProductImage to the loaded collection, so untracked it
/// would answer with a product missing the image it just accepted.
/// </summary>
public class ProductRepositoryTrackingTests : IDisposable
{
    private readonly CatalogTestContext _fixture = new();

    private async Task<Product> SeedProductAsync()
    {
        // "Pozorište", not one of CategorySeeder's Muzika/Sport/Tehnologija — Category.Name carries
        // a unique index and the seeder's rows are already in every fresh fixture database.
        var category = new Category { Name = "Pozorište", IsActive = true, TicketingMode = TicketingMode.SingleOccurrence };
        await _fixture.CategoryRepository.AddAsync(category);
        await _fixture.UnitOfWork.SaveChangesAsync();

        var product = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = "Koncert",
            Description = "Opis",
            City = City.Sarajevo,
            Status = PublishStatus.Published,
            Date = DateTime.UtcNow.AddMonths(1),
        };
        await _fixture.ProductRepository.AddAsync(product);
        await _fixture.UnitOfWork.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
        return product;
    }

    [Fact]
    public async Task GetByIdWithCategoryAsync_TracksTheProduct()
    {
        var product = await SeedProductAsync();

        await _fixture.ProductRepository.GetByIdWithCategoryAsync(product.Id);

        _fixture.DbContext.ChangeTracker.Entries<Product>()
            .Should().ContainSingle()
            .Which.State.Should().Be(EntityState.Unchanged);
    }

    [Fact]
    public async Task GetByIdWithCategoryNoTrackingAsync_DoesNotTrackTheProduct()
    {
        var product = await SeedProductAsync();

        await _fixture.ProductRepository.GetByIdWithCategoryNoTrackingAsync(product.Id);

        _fixture.DbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdWithCategoryNoTrackingAsync_StillEagerLoadsCategoryAndImages()
    {
        // The three read callers shape a response out of this: GetInternalAsync reads
        // Category.TicketingMode and GetByIdAsync maps Images into ImageUrls, so dropping either
        // Include would be a null-reference or an empty gallery, not a perf regression.
        var product = await SeedProductAsync();

        var loaded = await _fixture.ProductRepository.GetByIdWithCategoryNoTrackingAsync(product.Id);

        loaded.Should().NotBeNull();
        loaded!.Category.Should().NotBeNull();
        loaded.Images.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdWithCategoryNoTrackingAsync_ForUnknownId_ReturnsNull()
    {
        (await _fixture.ProductRepository.GetByIdWithCategoryNoTrackingAsync(Guid.NewGuid()))
            .Should().BeNull();
    }

    public void Dispose() => _fixture.Dispose();
}
