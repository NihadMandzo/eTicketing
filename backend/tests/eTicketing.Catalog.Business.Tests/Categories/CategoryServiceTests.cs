using eTicketing.Catalog.Business.Categories;
using eTicketing.Catalog.Business.Tests.TestFixtures;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace eTicketing.Catalog.Business.Tests.Categories;

public class CategoryServiceTests : IDisposable
{
    private readonly CatalogTestContext _fixture = new();
    private readonly ICategoryService _sut;

    public CategoryServiceTests()
    {
        _sut = _fixture.CreateCategoryService();
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static IFormFile CreateFormFile(byte[] bytes, string contentType = "image/png", string fileName = "icon.png")
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, "Icon", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private static CreateCategoryRequest ValidCreateRequest() => new()
    {
        Name = "Muzika",
        Description = "Koncerti i festivali",
        IsActive = true,
        DisplayOrder = 1,
    };

    [Fact]
    public async Task CreateAsync_CreatesCategoryWithNullIconUrl()
    {
        var result = await _sut.CreateAsync(ValidCreateRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.IconUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ForUnknownId_ReturnsNotFound()
    {
        var result = await _sut.GetByIdAsync(999);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("category.not_found");
    }

    [Fact]
    public async Task UpdateAsync_DoesNotChangeIconUrl()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.UploadIconAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(40, 40)));

        var updated = await _sut.UpdateAsync(created.Value.Id, new UpdateCategoryRequest
        {
            Name = "Muzika i Koncerti",
            Description = "Ažuriran opis",
            IsActive = true,
            DisplayOrder = 2,
        });

        updated.IsSuccess.Should().BeTrue();
        updated.Value!.Name.Should().Be("Muzika i Koncerti");
        updated.Value.IconUrl.Should().NotBeNull(); // metadata-only update leaves the icon alone
    }

    [Fact]
    public async Task UpdateAsync_ForUnknownId_ReturnsNotFound()
    {
        var result = await _sut.UpdateAsync(999, new UpdateCategoryRequest
        {
            Name = "X",
            Description = "",
            IsActive = true,
            DisplayOrder = 0,
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("category.not_found");
    }

    [Fact]
    public async Task DeleteAsync_RemovesCategory()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());

        var deleted = await _sut.DeleteAsync(created.Value!.Id);
        deleted.IsSuccess.Should().BeTrue();

        var fetched = await _sut.GetByIdAsync(created.Value.Id);
        fetched.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ForUnknownId_ReturnsNotFound()
    {
        var result = await _sut.DeleteAsync(999);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("category.not_found");
    }

    [Fact]
    public async Task DeleteAsync_ForCategoryReferencedByProduct_ReturnsConflict()
    {
        // Regression test for the fix this task made: deleting an in-use category used to throw
        // a raw DbUpdateException (FK Restrict on Product.CategoryId) surfaced as a 500, instead
        // of a normal domain conflict.
        var created = await _sut.CreateAsync(ValidCreateRequest());
        await _fixture.ProductRepository.AddAsync(new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            OrganizationId = Guid.NewGuid(),
            CategoryId = created.Value!.Id,
            Date = DateTime.UtcNow.AddDays(5),
            Status = PublishStatus.Published,
        });
        await _fixture.UnitOfWork.SaveChangesAsync();

        var result = await _sut.DeleteAsync(created.Value.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("category.in_use");

        var stillExists = await _sut.GetByIdAsync(created.Value.Id);
        stillExists.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ForCategoryWithIcon_DeletesBlobToo()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        var uploaded = await _sut.UploadIconAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(50, 50)));
        // PathOf first: the URL carries a ?v= cache-busting token that is not part of the key.
        var blobName = PathOf(uploaded.Value!.IconUrl)!.Split('/').Last();
        _fixture.BlobStorage.Exists("category-icons", blobName).Should().BeTrue();

        await _sut.DeleteAsync(created.Value.Id);

        _fixture.BlobStorage.Exists("category-icons", blobName).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenBlobDeleteFails_StillRemovesCategoryRow()
    {
        // Regression test for the fix this task made: the blob is now deleted only after the DB
        // delete commits, so a blob-storage failure (e.g. transient Azure outage) can no longer
        // leave the category row alive while pointing at an already-deleted blob.
        var created = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.UploadIconAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(50, 50)));
        _fixture.BlobStorage.ThrowOnDelete = true;

        var act = () => _sut.DeleteAsync(created.Value.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        var fetched = await _sut.GetByIdAsync(created.Value.Id);
        fetched.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UploadIconAsync_ForCategoryWithoutIcon_StoresBlobAndReturnsUrl()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());

        var result = await _sut.UploadIconAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(80, 80)));

        result.IsSuccess.Should().BeTrue();
        result.Value!.IconUrl.Should().NotBeNull();
        result.Value.IconUrl.Should().Contain("category-icons");
    }

    [Fact]
    public async Task UploadIconAsync_ForCategoryThatAlreadyHasIcon_ReturnsConflict()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.UploadIconAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(40, 40)));

        var result = await _sut.UploadIconAsync(created.Value.Id, CreateFormFile(CreatePngBytes(40, 40)));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("category.icon_already_exists");
    }

    [Fact]
    public async Task UploadIconAsync_ForUnknownId_ReturnsNotFound()
    {
        var result = await _sut.UploadIconAsync(999, CreateFormFile(CreatePngBytes(40, 40)));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("category.not_found");
    }

    [Fact]
    public async Task ReplaceIconAsync_ForCategoryWithIcon_OverwritesSameBlobKey()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        var uploaded = await _sut.UploadIconAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(40, 40)));
        var originalUrl = uploaded.Value!.IconUrl;

        var replaced = await _sut.ReplaceIconAsync(created.Value.Id, CreateFormFile(CreatePngBytes(60, 60)));

        replaced.IsSuccess.Should().BeTrue();
        // Same blob key, just overwritten — compared without the ?v= cache-busting token, which
        // is expected to differ (see the test below).
        PathOf(replaced.Value!.IconUrl).Should().Be(PathOf(originalUrl));
    }

    [Fact]
    public async Task ReplaceIconAsync_ChangesTheIconUrlsVersionToken()
    {
        // The regression this guards: a replace writes to the same blob key, so the URL used to
        // come back byte-identical and every client kept serving the icon it had already cached.
        // ReplaceIconAsync now touches the row so UpdatedAt advances, and BuildIconUrl stamps it.
        var created = await _sut.CreateAsync(ValidCreateRequest());
        var uploaded = await _sut.UploadIconAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(40, 40)));
        var originalUrl = uploaded.Value!.IconUrl;

        var replaced = await _sut.ReplaceIconAsync(created.Value.Id, CreateFormFile(CreatePngBytes(60, 60)));

        replaced.IsSuccess.Should().BeTrue();
        replaced.Value!.IconUrl.Should().NotBe(originalUrl);
        replaced.Value.IconUrl.Should().Contain("?v=");
    }

    /// <summary>The URL without its ?v= cache-busting token — i.e. the blob key part.</summary>
    private static string? PathOf(string? url) => url?.Split('?')[0];

    [Fact]
    public async Task ReplaceIconAsync_ForCategoryWithoutIcon_ReturnsNotFound()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());

        var result = await _sut.ReplaceIconAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(40, 40)));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("category.icon_not_found");
    }

    [Fact]
    public async Task ReplaceIconAsync_ForUnknownId_ReturnsNotFound()
    {
        var result = await _sut.ReplaceIconAsync(999, CreateFormFile(CreatePngBytes(40, 40)));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("category.not_found");
    }

    [Fact]
    public async Task CreateAsync_WithExplicitTicketingMode_PersistsIt()
    {
        var result = await _sut.CreateAsync(ValidCreateRequest() with { TicketingMode = TicketingMode.DailyEntry });

        result.IsSuccess.Should().BeTrue();
        result.Value!.TicketingMode.Should().Be(TicketingMode.DailyEntry);
    }

    [Fact]
    public async Task CreateAsync_WithoutExplicitTicketingMode_DefaultsToSingleOccurrence()
    {
        var result = await _sut.CreateAsync(ValidCreateRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.TicketingMode.Should().Be(TicketingMode.SingleOccurrence);
    }

    [Fact]
    public async Task UpdateAsync_CanChangeTicketingMode()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());

        var updated = await _sut.UpdateAsync(created.Value!.Id, new UpdateCategoryRequest
        {
            Name = created.Value.Name,
            Description = created.Value.Description,
            IsActive = true,
            DisplayOrder = created.Value.DisplayOrder,
            TicketingMode = TicketingMode.RecurringReservation,
        });

        updated.IsSuccess.Should().BeTrue();
        updated.Value!.TicketingMode.Should().Be(TicketingMode.RecurringReservation);
    }

    [Fact]
    public async Task GetAsync_FiltersByFtsOnName()
    {
        // CategorySeeder.cs seeds Muzika/Sport/Tehnologija into every fresh Sqlite in-memory DB
        // (EnsureCreated() applies HasData too) — use a name that can't collide with those seed
        // rows.
        await _sut.CreateAsync(ValidCreateRequest());
        await _sut.CreateAsync(new CreateCategoryRequest
        {
            Name = "Pozorište",
            Description = "Pozorišne predstave",
            IsActive = true,
            DisplayOrder = 2,
        });

        var result = await _sut.GetAsync(new CategoryQuery { FTS = "Pozorište" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().ContainSingle(c => c.Name == "Pozorište");
    }

    public void Dispose() => _fixture.Dispose();
}
