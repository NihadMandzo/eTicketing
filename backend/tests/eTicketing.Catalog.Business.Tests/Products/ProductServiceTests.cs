using System.Security.Claims;
using eTicketing.Catalog.Business.Products;
using eTicketing.Catalog.Business.Tests.TestFixtures;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace eTicketing.Catalog.Business.Tests.Products;

public class ProductServiceTests : IDisposable
{
    private readonly CatalogTestContext _fixture = new();
    private readonly IProductService _sut;

    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();
    private Category _music = null!;
    private Category _sport = null!;
    private Category _dailyEntryCategory = null!;

    public ProductServiceTests()
    {
        _sut = _fixture.CreateProductService();
        SeedCategoriesAndProducts().GetAwaiter().GetResult();
    }

    /// <summary>Builds a ClaimsPrincipal matching the claim shape JwtTokenGenerator mints
    /// (ClaimTypes.NameIdentifier/Role + a plain "organizationId" claim), for exercising
    /// ProductService's ownership checks without going through real JWT issuance.</summary>
    private static ClaimsPrincipal BuildCaller(string role, Guid? organizationId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
        };
        if (organizationId is not null)
            claims.Add(new Claim("organizationId", organizationId.Value.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private ClaimsPrincipal OrgACaller() => BuildCaller("OrganizationSuperAdmin", _orgA);
    private ClaimsPrincipal OrgBCaller() => BuildCaller("OrganizationSuperAdmin", _orgB);
    private static ClaimsPrincipal PlatformStaffCaller() => BuildCaller("SuperAdmin");

    private async Task SeedCategoriesAndProducts()
    {
        _music = new Category { Name = "Muzika", IsActive = true, TicketingMode = TicketingMode.SingleOccurrence };
        _sport = new Category { Name = "Sport", IsActive = true, TicketingMode = TicketingMode.SingleOccurrence };
        _dailyEntryCategory = new Category { Name = "Muzej", IsActive = true, TicketingMode = TicketingMode.DailyEntry };
        await _fixture.CategoryRepository.AddAsync(_music);
        await _fixture.CategoryRepository.AddAsync(_sport);
        await _fixture.CategoryRepository.AddAsync(_dailyEntryCategory);
        await _fixture.UnitOfWork.SaveChangesAsync();

        await _fixture.ProductRepository.AddAsync(new Product
        {
            Id = Guid.NewGuid(),
            Name = "Ljetni Festival",
            OrganizationId = _orgA,
            CategoryId = _music.Id,
            Date = DateTime.UtcNow.AddDays(10),
            Status = PublishStatus.Draft,
        });
        await _fixture.ProductRepository.AddAsync(new Product
        {
            Id = Guid.NewGuid(),
            Name = "Košarkaški Turnir",
            OrganizationId = _orgA,
            CategoryId = _sport.Id,
            Date = DateTime.UtcNow.AddDays(20),
            Status = PublishStatus.Published,
        });
        await _fixture.ProductRepository.AddAsync(new Product
        {
            Id = Guid.NewGuid(),
            Name = "Jesenji Koncert",
            OrganizationId = _orgB,
            CategoryId = _music.Id,
            Date = DateTime.UtcNow.AddDays(30),
            Status = PublishStatus.Published,
        });
        await _fixture.UnitOfWork.SaveChangesAsync();
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static IFormFile CreateFormFile(byte[] bytes, string contentType = "image/png", string fileName = "image.png")
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, "Image", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private static UpsertProductRequest ValidRequest(int categoryId) => new()
    {
        Name = "Novi Proizvod",
        Description = "Opis",
        Date = DateTime.UtcNow.AddDays(5),
        CategoryId = categoryId,
    };

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_HappyPath_CreatesAsDraftWithOrganizationIdFromClaim()
    {
        var result = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PublishStatus.Draft);
        result.Value.OrganizationId.Should().Be(_orgA);
        result.Value.CategoryName.Should().Be("Muzika");
    }

    [Fact]
    public async Task CreateAsync_ForSingleOccurrenceCategory_WithNullDate_ReturnsValidationError()
    {
        var request = ValidRequest(_music.Id) with { Date = null };

        var result = await _sut.CreateAsync(request, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.date_required");
    }

    [Fact]
    public async Task CreateAsync_ForSingleOccurrenceCategory_WithPastDate_ReturnsValidationError()
    {
        var request = ValidRequest(_music.Id) with { Date = DateTime.UtcNow.AddDays(-1) };

        var result = await _sut.CreateAsync(request, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.date_required");
    }

    [Fact]
    public async Task CreateAsync_ForDailyEntryCategory_WithDate_ReturnsValidationError()
    {
        var request = ValidRequest(_dailyEntryCategory.Id) with { Date = DateTime.UtcNow.AddDays(5) };

        var result = await _sut.CreateAsync(request, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.date_not_applicable");
    }

    [Fact]
    public async Task CreateAsync_ForDailyEntryCategory_WithNullDate_Succeeds()
    {
        var request = ValidRequest(_dailyEntryCategory.Id) with { Date = null };

        var result = await _sut.CreateAsync(request, OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Date.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ForUnknownCategory_ReturnsValidationError()
    {
        var result = await _sut.CreateAsync(ValidRequest(999), OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.category_not_found");
    }

    // --- PreviewAsync ---

    [Fact]
    public async Task PreviewAsync_DoesNotPersist()
    {
        var before = await _sut.GetAllAsync(new ProductQuery());

        var result = await _sut.PreviewAsync(ValidRequest(_music.Id), OrgACaller());

        result.IsSuccess.Should().BeTrue();
        var after = await _sut.GetAllAsync(new ProductQuery());
        after.Value!.TotalCount.Should().Be(before.Value!.TotalCount);
    }

    [Fact]
    public async Task PreviewAsync_UsesSameValidationAsCreateAsync()
    {
        var request = ValidRequest(_music.Id) with { Date = null };

        var result = await _sut.PreviewAsync(request, OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.date_required");
    }

    // --- PublishAsync ---

    [Fact]
    public async Task PublishAsync_ForOwnDraft_SetsStatusToPublished()
    {
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());

        var result = await _sut.PublishAsync(created.Value!.Id, OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(PublishStatus.Published);
    }

    [Fact]
    public async Task PublishAsync_ForAnotherOrganizationsProduct_ReturnsUnauthorized()
    {
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());

        var result = await _sut.PublishAsync(created.Value!.Id, OrgBCaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.forbidden");
    }

    [Fact]
    public async Task PublishAsync_ForAnotherOrganizationsProduct_ByPlatformStaff_Succeeds()
    {
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());

        var result = await _sut.PublishAsync(created.Value!.Id, PlatformStaffCaller());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_ForUnknownId_ReturnsNotFound()
    {
        var result = await _sut.PublishAsync(Guid.NewGuid(), OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.not_found");
    }

    // --- UpdateAsync ---

    [Fact]
    public async Task UpdateAsync_ForPublishedProduct_KeepsItPublished()
    {
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());
        await _sut.PublishAsync(created.Value!.Id, OrgACaller());

        var updated = await _sut.UpdateAsync(created.Value.Id, ValidRequest(_music.Id) with { Name = "Izmijenjeno" }, OrgACaller());

        updated.IsSuccess.Should().BeTrue();
        updated.Value!.Status.Should().Be(PublishStatus.Published);
        updated.Value.Name.Should().Be("Izmijenjeno");
    }

    [Fact]
    public async Task UpdateAsync_ForAnotherOrganizationsProduct_ReturnsUnauthorized()
    {
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());

        var result = await _sut.UpdateAsync(created.Value!.Id, ValidRequest(_music.Id), OrgBCaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.forbidden");
    }

    // --- DeleteAsync ---

    [Fact]
    public async Task DeleteAsync_ForOwnProduct_RemovesIt()
    {
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());

        var result = await _sut.DeleteAsync(created.Value!.Id, OrgACaller());

        result.IsSuccess.Should().BeTrue();
        var fetched = await _sut.GetInternalAsync(created.Value.Id);
        fetched.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ForAnotherOrganizationsProduct_ReturnsUnauthorized()
    {
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());

        var result = await _sut.DeleteAsync(created.Value!.Id, OrgBCaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.forbidden");
    }

    // --- GetPublishedAsync / GetMineAsync / GetAllAsync ---

    [Fact]
    public async Task GetPublishedAsync_ReturnsOnlyPublishedAcrossAllOrganizations()
    {
        var result = await _sut.GetPublishedAsync(new ProductQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().OnlyContain(p => p.Status == PublishStatus.Published);
        result.Value.Items.Should().Contain(p => p.Name == "Košarkaški Turnir");
        result.Value.Items.Should().NotContain(p => p.Name == "Ljetni Festival"); // Draft
    }

    [Fact]
    public async Task GetMineAsync_ReturnsOnlyCallersOrganizationRegardlessOfStatus()
    {
        var result = await _sut.GetMineAsync(new ProductQuery(), OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().OnlyContain(p => p.OrganizationId == _orgA);
        result.Value.Items.Should().HaveCount(2); // includes the Draft one
    }

    [Fact]
    public async Task GetAllAsync_FiltersByOrganizationAndCategory()
    {
        var result = await _sut.GetAllAsync(new ProductQuery { OrganizationId = _orgA, CategoryId = _music.Id });

        result.Value!.Items.Should().ContainSingle(p => p.Name == "Ljetni Festival");
    }

    [Fact]
    public async Task GetAllAsync_FiltersByFtsOnName()
    {
        var result = await _sut.GetAllAsync(new ProductQuery { FTS = "Košarkaški" });

        result.Value!.Items.Should().ContainSingle(p => p.Name == "Košarkaški Turnir");
    }

    [Fact]
    public async Task GetOrganizationIdsAsync_ReturnsDistinctOrganizationIdsAcrossStatuses()
    {
        // Muzika products span both orgs (_orgA Draft, _orgB Published) — the org-list category
        // filter must include both statuses, not just Published, since this is an internal
        // admin-facing filter rather than the public catalog.
        var result = await _sut.GetOrganizationIdsAsync([_music.Id]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo([_orgA, _orgB]);
    }

    // --- GetInternalAsync ---

    [Fact]
    public async Task GetInternalAsync_ReturnsTicketingModeFromOwningCategory()
    {
        var created = await _sut.CreateAsync(ValidRequest(_dailyEntryCategory.Id) with { Date = null }, OrgACaller());

        var result = await _sut.GetInternalAsync(created.Value!.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TicketingMode.Should().Be(TicketingMode.DailyEntry);
        result.Value.OrganizationId.Should().Be(_orgA);
    }

    // --- UploadImageAsync / DeleteImageAsync ---

    [Fact]
    public async Task UploadImageAsync_ForOwnProduct_StoresBlobAndReturnsUrl()
    {
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());

        var result = await _sut.UploadImageAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(80, 60)), OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Images.Should().ContainSingle();
        result.Value.Images[0].Url.Should().Contain("product-images");
    }

    [Fact]
    public async Task UploadImageAsync_ForAnotherOrganizationsProduct_ReturnsUnauthorized()
    {
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());

        var result = await _sut.UploadImageAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(80, 60)), OrgBCaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.forbidden");
    }

    [Fact]
    public async Task UploadImageAsync_ForUnknownId_ReturnsNotFound()
    {
        var result = await _sut.UploadImageAsync(Guid.NewGuid(), CreateFormFile(CreatePngBytes(80, 60)), OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.not_found");
    }

    [Fact]
    public async Task UploadImageAsync_WhenAlreadyAtFiveImages_ReturnsConflict()
    {
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());
        for (var i = 0; i < 5; i++)
        {
            await _sut.UploadImageAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(40, 40)), OrgACaller());
        }

        var result = await _sut.UploadImageAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(40, 40)), OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.images_limit_reached");
    }

    [Fact]
    public async Task UploadImageAsync_AfterNonSequentialDelete_AssignsDisplayOrderPastMaxNotCount()
    {
        // Regression test for PR #19 review feedback: DisplayOrder must be derived from the max
        // existing value, not Images.Count — deleting a middle image (index 1 of 0,1,2) leaves
        // survivors {0,2} with Count == 2, which used to collide with the surviving DisplayOrder=2
        // image on the next upload.
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());
        await _sut.UploadImageAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(40, 40)), OrgACaller()); // DisplayOrder 0
        var second = await _sut.UploadImageAsync(created.Value.Id, CreateFormFile(CreatePngBytes(40, 40)), OrgACaller()); // DisplayOrder 1
        await _sut.UploadImageAsync(created.Value.Id, CreateFormFile(CreatePngBytes(40, 40)), OrgACaller()); // DisplayOrder 2
        var middleImageId = second.Value!.Images.Single(i => i.DisplayOrder == 1).Id;

        await _sut.DeleteImageAsync(created.Value.Id, middleImageId, OrgACaller());
        var afterUpload = await _sut.UploadImageAsync(created.Value.Id, CreateFormFile(CreatePngBytes(40, 40)), OrgACaller());

        afterUpload.IsSuccess.Should().BeTrue();
        afterUpload.Value!.Images.Select(i => i.DisplayOrder).Should().OnlyHaveUniqueItems();
        afterUpload.Value.Images.Should().Contain(i => i.DisplayOrder == 3);
    }

    [Fact]
    public async Task DeleteImageAsync_ForOwnProduct_RemovesImageAndBlob()
    {
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());
        var uploaded = await _sut.UploadImageAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(80, 60)), OrgACaller());
        var imageId = uploaded.Value!.Images[0].Id;
        var blobName = uploaded.Value.Images[0].Url.Split('/').Last();
        _fixture.BlobStorage.Exists("product-images", blobName).Should().BeTrue();

        var result = await _sut.DeleteImageAsync(created.Value.Id, imageId, OrgACaller());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Images.Should().BeEmpty();
        _fixture.BlobStorage.Exists("product-images", blobName).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteImageAsync_ForUnknownImageId_ReturnsNotFound()
    {
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());

        var result = await _sut.DeleteImageAsync(created.Value!.Id, Guid.NewGuid(), OrgACaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.image_not_found");
    }

    [Fact]
    public async Task DeleteImageAsync_ForAnotherOrganizationsProduct_ReturnsUnauthorized()
    {
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());
        var uploaded = await _sut.UploadImageAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(80, 60)), OrgACaller());

        var result = await _sut.DeleteImageAsync(created.Value.Id, uploaded.Value!.Images[0].Id, OrgBCaller());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.forbidden");
    }

    [Fact]
    public async Task DeleteAsync_ForProductWithImages_DeletesBlobsToo()
    {
        var created = await _sut.CreateAsync(ValidRequest(_music.Id), OrgACaller());
        var uploaded = await _sut.UploadImageAsync(created.Value!.Id, CreateFormFile(CreatePngBytes(80, 60)), OrgACaller());
        var blobName = uploaded.Value!.Images[0].Url.Split('/').Last();
        _fixture.BlobStorage.Exists("product-images", blobName).Should().BeTrue();

        await _sut.DeleteAsync(created.Value.Id, OrgACaller());

        _fixture.BlobStorage.Exists("product-images", blobName).Should().BeFalse();
    }

    public void Dispose() => _fixture.Dispose();
}
