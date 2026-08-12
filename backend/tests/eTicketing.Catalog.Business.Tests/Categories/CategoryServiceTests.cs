using eTicketing.Catalog.Business.Categories;
using eTicketing.Catalog.Business.Tests.TestFixtures;
using eTicketing.Contracts.Pagination;
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

    private static CreateCategoryRequest ValidCreateRequest(byte[]? iconBytes = null) => new()
    {
        Name = "Muzika",
        Description = "Koncerti i festivali",
        IsActive = true,
        DisplayOrder = 1,
        Icon = CreateFormFile(iconBytes ?? CreatePngBytes(50, 50)),
    };

    [Fact]
    public async Task CreateAsync_BuildsIconUrlFromPublicBaseUrl()
    {
        var result = await _sut.CreateAsync(ValidCreateRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.IconUrl.Should().Be($"http://localhost:5000/api/categories/{result.Value.Id}/icon");
    }

    [Fact]
    public async Task CreateAsync_StoresIconBytesAndContentType()
    {
        var iconBytes = CreatePngBytes(80, 80);

        var created = await _sut.CreateAsync(ValidCreateRequest(iconBytes));
        var icon = await _sut.GetIconAsync(created.Value!.Id);

        icon.IsSuccess.Should().BeTrue();
        icon.Value!.ContentType.Should().Be("image/png");
        icon.Value.Data.Should().BeEquivalentTo(iconBytes);
    }

    [Fact]
    public async Task GetByIdAsync_ForUnknownId_ReturnsNotFound()
    {
        var result = await _sut.GetByIdAsync(999);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("category.not_found");
    }

    [Fact]
    public async Task GetIconAsync_ForUnknownId_ReturnsNotFound()
    {
        var result = await _sut.GetIconAsync(999);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("category.not_found");
    }

    [Fact]
    public async Task UpdateAsync_WithoutNewIcon_KeepsExistingIconBytes()
    {
        var originalIcon = CreatePngBytes(40, 40);
        var created = await _sut.CreateAsync(ValidCreateRequest(originalIcon));

        var updated = await _sut.UpdateAsync(created.Value!.Id, new UpdateCategoryRequest
        {
            Name = "Muzika i Koncerti",
            Description = "Ažuriran opis",
            IsActive = true,
            DisplayOrder = 2,
            Icon = null,
        });

        updated.IsSuccess.Should().BeTrue();
        updated.Value!.Name.Should().Be("Muzika i Koncerti");

        var icon = await _sut.GetIconAsync(created.Value.Id);
        icon.Value!.Data.Should().BeEquivalentTo(originalIcon);
    }

    [Fact]
    public async Task UpdateAsync_WithNewIcon_ReplacesIconBytes()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest(CreatePngBytes(40, 40)));
        var replacementIcon = CreatePngBytes(60, 60);

        await _sut.UpdateAsync(created.Value!.Id, new UpdateCategoryRequest
        {
            Name = "Muzika",
            Description = "Opis",
            IsActive = true,
            DisplayOrder = 1,
            Icon = CreateFormFile(replacementIcon),
        });

        var icon = await _sut.GetIconAsync(created.Value.Id);
        icon.Value!.Data.Should().BeEquivalentTo(replacementIcon);
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
    public async Task GetAsync_FiltersByFtsOnName()
    {
        // CategoryConfiguration.HasData() seeds Muzika/Sport/Tehnologija into every fresh
        // Sqlite in-memory DB (EnsureCreated() applies HasData too) — use a name that can't
        // collide with those seed rows.
        await _sut.CreateAsync(ValidCreateRequest());
        await _sut.CreateAsync(new CreateCategoryRequest
        {
            Name = "Pozorište",
            Description = "Pozorišne predstave",
            IsActive = true,
            DisplayOrder = 2,
            Icon = CreateFormFile(CreatePngBytes(50, 50)),
        });

        var result = await _sut.GetAsync(new CategoryQuery { FTS = "Pozorište" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().ContainSingle(c => c.Name == "Pozorište");
    }

    public void Dispose() => _fixture.Dispose();
}
