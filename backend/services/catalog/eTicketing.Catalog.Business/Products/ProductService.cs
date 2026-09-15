using System.Security.Claims;
using eTicketing.Catalog.Business.Products.Mapping;
using eTicketing.Catalog.Business.Products.Validators;
using eTicketing.Contracts.Security;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Events;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Shared.Storage;
using Mapster;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Products;

public class ProductService : IProductService
{
    private const string ContainerName = "product-images";

    private readonly IProductRepository _productRepository;
    private readonly IProductImageRepository _productImageRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ProductResponseFactory _responseFactory;

    public ProductService(
        IProductRepository productRepository,
        IProductImageRepository productImageRepository,
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        IBlobStorageService blobStorageService,
        IEventPublisher eventPublisher,
        ProductResponseFactory responseFactory)
    {
        _productRepository = productRepository;
        _productImageRepository = productImageRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _blobStorageService = blobStorageService;
        _eventPublisher = eventPublisher;
        _responseFactory = responseFactory;
    }

    public async Task<Result<ProductPreviewResponse>> PreviewAsync(UpsertProductRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(request, ct);
        if (validation.IsFailure)
            return Result<ProductPreviewResponse>.Failure(validation.Error);

        var category = validation.Value!;
        return Result<ProductPreviewResponse>.Success(new ProductPreviewResponse(
            request.Name, request.Description, request.Date, category.Id, category.Name, category.TicketingMode,
            request.Latitude!.Value, request.Longitude!.Value, request.City!.Value));
    }

    public async Task<Result<ProductResponse>> CreateAsync(UpsertProductRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(request, ct);
        if (validation.IsFailure)
            return Result<ProductResponse>.Failure(validation.Error);

        var organizationId = user.GetOrganizationId();
        if (organizationId is null)
            return Result<ProductResponse>.Failure(Error.Unauthorized("product.no_organization", "Nalog nije vezan za organizaciju."));

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Date = request.Date,
            CategoryId = request.CategoryId,
            OrganizationId = organizationId.Value,
            Status = PublishStatus.Draft,
            Latitude = request.Latitude!.Value,
            Longitude = request.Longitude!.Value,
            City = request.City!.Value,
        };

        await _productRepository.AddAsync(product, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        product.Category = validation.Value;
        return Result<ProductResponse>.Success(ToResponse(product));
    }

    public async Task<Result<ProductResponse>> PublishAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdWithCategoryAsync(id, ct);
        if (product is null)
            return Result<ProductResponse>.Failure(Error.NotFound("product.not_found", "Proizvod nije pronađen."));

        var ownershipError = AuthorizeOwnership(user, product.OrganizationId);
        if (ownershipError is not null)
            return Result<ProductResponse>.Failure(ownershipError);

        product.Status = PublishStatus.Published;
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<ProductResponse>.Success(ToResponse(product));
    }

    public async Task<Result<ProductResponse>> UpdateAsync(Guid id, UpsertProductRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdWithCategoryAsync(id, ct);
        if (product is null)
            return Result<ProductResponse>.Failure(Error.NotFound("product.not_found", "Proizvod nije pronađen."));

        var ownershipError = AuthorizeOwnership(user, product.OrganizationId);
        if (ownershipError is not null)
            return Result<ProductResponse>.Failure(ownershipError);

        var validation = await ValidateAsync(request, ct);
        if (validation.IsFailure)
            return Result<ProductResponse>.Failure(validation.Error);

        // Diffed BEFORE the assignments below overwrite the old values, and only for a published
        // product: a draft has no buyers by construction, so there is nobody to notify and no
        // reason to put an event on the bus.
        var changes = product.Status == PublishStatus.Published
            ? ProductChangeDetector.Detect(product, request, product.Category?.Name ?? string.Empty, validation.Value!.Name)
            : [];

        // A Published product stays Published after an edit — Status is deliberately untouched here.
        product.Name = request.Name;
        product.Description = request.Description;
        product.Date = request.Date;
        product.CategoryId = request.CategoryId;
        product.Category = validation.Value;
        product.Latitude = request.Latitude!.Value;
        product.Longitude = request.Longitude!.Value;
        product.City = request.City!.Value;

        await _unitOfWork.SaveChangesAsync(ct);

        // Published after the commit, never before: a buyer must not be emailed "the date moved"
        // about a save that then failed. eTicketing.Ticketing consumes this and fans it out to the
        // actual ticket holders — Catalog has no idea who they are.
        if (changes.Count > 0)
        {
            await _eventPublisher.PublishAsync(
                EventNames.ProductUpdated,
                new ProductUpdated(product.Id, product.Name, DateTime.UtcNow, changes),
                ct);
        }

        return Result<ProductResponse>.Success(ToResponse(product));
    }

    public async Task<Result> DeleteAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        // GetByIdWithCategoryAsync also Includes Images — needed below to delete their blobs.
        var product = await _productRepository.GetByIdWithCategoryAsync(id, ct);
        if (product is null)
            return Result.Failure(Error.NotFound("product.not_found", "Proizvod nije pronađen."));

        var ownershipError = AuthorizeOwnership(user, product.OrganizationId);
        if (ownershipError is not null)
            return Result.Failure(ownershipError);

        var blobNames = product.Images.Select(i => i.BlobName).ToList();

        // Captured before Remove: after the delete these live only on a detached entity, and the
        // notification below needs them to say what was cancelled and to whom.
        var deleted = new ProductDeleted(
            ProductId: product.Id,
            ProductName: product.Name,
            OrganizationId: product.OrganizationId,
            ProductDate: product.Date,
            DeletedAt: DateTime.UtcNow,
            // Platform staff deleting someone else's product is the case the organization has to
            // hear about. An organizer deleting their own is told nothing — they just did it.
            DeletedByPlatformStaff: user.IsPlatformStaff() && user.GetOrganizationId() != product.OrganizationId);

        _productRepository.Remove(product);
        await _unitOfWork.SaveChangesAsync(ct);

        // After the commit, never before — the same rule UpdateAsync follows. Nobody may be told
        // their event is cancelled on the strength of a delete that then failed to commit.
        // eTicketing.Ticketing consumes this and fans it out: Catalog has no idea who bought a
        // ticket. Published for drafts too, which have no buyers but whose owning organization
        // still needs telling when platform staff removed one.
        await _eventPublisher.PublishAsync(EventNames.ProductDeleted, deleted, ct);

        // DB delete first (cascades ProductImage rows too, see ProductImageConfiguration): if
        // SaveChangesAsync above throws, every blob is left untouched rather than orphaned while
        // the product row is still alive and pointing at them. If a blob delete below throws
        // instead (genuine Azure outage) — after the DB commit already succeeded — the product is
        // gone but that blob lingers; acceptable and recoverable, unlike the reverse. Mirrors
        // CategoryService.DeleteAsync / OrganizationService.DeleteAsync exactly.
        foreach (var blobName in blobNames)
        {
            await _blobStorageService.DeleteAsync(ContainerName, blobName, ct);
        }

        return Result.Success();
    }

    public async Task<Result<ProductResponse>> UploadImageAsync(Guid id, IFormFile image, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdWithCategoryAsync(id, ct);
        if (product is null)
            return Result<ProductResponse>.Failure(Error.NotFound("product.not_found", "Proizvod nije pronađen."));

        var ownershipError = AuthorizeOwnership(user, product.OrganizationId);
        if (ownershipError is not null)
            return Result<ProductResponse>.Failure(ownershipError);

        if (product.Images.Count >= ProductImageValidation.MaxCount)
        {
            return Result<ProductResponse>.Failure(Error.Conflict(
                "product.images_limit_reached",
                $"Proizvod već ima maksimalan broj slika ({ProductImageValidation.MaxCount})."));
        }

        // DisplayOrder must be derived from the max existing value, not Count — after a
        // non-sequential delete (e.g. images at 0,1,2, delete index 1 -> survivors {0,2},
        // Count == 2), Count would collide with the surviving DisplayOrder=2 image and break
        // deterministic ordering/cover-image selection.
        var nextDisplayOrder = product.Images.Count == 0 ? 0 : product.Images.Max(i => i.DisplayOrder) + 1;
        var productImage = new ProductImage { Id = Guid.NewGuid(), ProductId = product.Id, DisplayOrder = nextDisplayOrder };
        var extension = await ProductImageValidation.DetectExtensionAsync(image, ct);
        productImage.BlobName = BlobNaming.BuildBlobName(productImage.Id, product.Name, extension);

        await _blobStorageService.UploadAsync(ContainerName, productImage.BlobName, image.OpenReadStream(), ContentTypeFor(extension), ct);

        // Goes through IProductImageRepository.AddAsync (DbSet.AddAsync) rather than
        // product.Images.Add(productImage) — EF Core's change tracker would otherwise mistake
        // this brand-new row for an update, since ProductImage.Id is a manually-assigned (not
        // store-generated-and-empty) key discovered via navigation fixup rather than an explicit
        // Add() call, which EF treats as "probably already exists" and issues an UPDATE that
        // affects 0 rows. EF's own relationship fixup then appends it into the already-loaded
        // product.Images collection automatically — adding it there manually too would leave a
        // duplicate entry in that in-memory list (same tracked instance twice).
        await _productImageRepository.AddAsync(productImage, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<ProductResponse>.Success(ToResponse(product));
    }

    public async Task<Result<ProductResponse>> DeleteImageAsync(Guid id, Guid imageId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdWithCategoryAsync(id, ct);
        if (product is null)
            return Result<ProductResponse>.Failure(Error.NotFound("product.not_found", "Proizvod nije pronađen."));

        var ownershipError = AuthorizeOwnership(user, product.OrganizationId);
        if (ownershipError is not null)
            return Result<ProductResponse>.Failure(ownershipError);

        var image = product.Images.FirstOrDefault(i => i.Id == imageId);
        if (image is null)
            return Result<ProductResponse>.Failure(Error.NotFound("product.image_not_found", "Slika nije pronađena."));

        // Same DB-first-then-blob ordering as DeleteAsync above.
        product.Images.Remove(image);
        await _unitOfWork.SaveChangesAsync(ct);
        await _blobStorageService.DeleteAsync(ContainerName, image.BlobName, ct);

        return Result<ProductResponse>.Success(ToResponse(product));
    }

    public async Task<Result<PagedResult<ProductResponse>>> GetPublishedAsync(ProductQuery query, CancellationToken ct = default)
    {
        var paged = await _productRepository.SearchAsync(query, query.OrganizationId, query.CategoryId, PublishStatus.Published, query.City, ct);
        return ToPagedResult(paged);
    }

    public async Task<Result<ProductResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdWithCategoryNoTrackingAsync(id, ct);
        if (product is null || product.Status != PublishStatus.Published)
            return Result<ProductResponse>.Failure(Error.NotFound("product.not_found", "Proizvod nije pronađen."));

        return Result<ProductResponse>.Success(ToResponse(product));
    }

    public async Task<Result<PagedResult<ProductResponse>>> GetMineAsync(ProductQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var organizationId = user.GetOrganizationId();
        if (organizationId is null)
            return Result<PagedResult<ProductResponse>>.Failure(Error.Unauthorized("product.no_organization", "Nalog nije vezan za organizaciju."));

        var paged = await _productRepository.SearchAsync(query, organizationId, query.CategoryId, query.Status, query.City, ct);
        return ToPagedResult(paged);
    }

    public async Task<Result<PagedResult<ProductResponse>>> GetAllAsync(ProductQuery query, CancellationToken ct = default)
    {
        var paged = await _productRepository.SearchAsync(query, query.OrganizationId, query.CategoryId, query.Status, query.City, ct);
        return ToPagedResult(paged);
    }

    public async Task<Result<List<Guid>>> GetOrganizationIdsAsync(IReadOnlyList<int> categoryIds, CancellationToken ct = default)
    {
        // No categories selected is a legitimate "filter nothing", not an error or a full scan —
        // this short-circuit used to live in the endpoint handler.
        if (categoryIds.Count == 0)
            return Result<List<Guid>>.Success([]);

        var ids = await _productRepository.GetOrganizationIdsByCategoryIdsAsync(categoryIds, ct);
        return Result<List<Guid>>.Success(ids);
    }

    public async Task<Result<ProductInternalResponse>> GetInternalAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdWithCategoryNoTrackingAsync(id, ct);
        if (product is null)
            return Result<ProductInternalResponse>.Failure(Error.NotFound("product.not_found", "Proizvod nije pronađen."));

        return Result<ProductInternalResponse>.Success(ToInternalResponse(product));
    }

    /// <summary>Upper bound on <see cref="GetInternalByIdsAsync"/>. Generous next to the real
    /// workload (an organizer validating a few dozen products in one day) but finite: the id list
    /// arrives as a bare List&lt;Guid&gt; body rather than a validated request DTO, so this is the
    /// only thing standing between a caller bug and an unbounded IN (...) clause.</summary>
    private const int MaxInternalByIdsCount = 200;

    public async Task<Result<List<ProductInternalResponse>>> GetInternalByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return Result<List<ProductInternalResponse>>.Success([]);

        if (ids.Count > MaxInternalByIdsCount)
        {
            return Result<List<ProductInternalResponse>>.Failure(Error.Validation(
                "product.too_many_ids", $"Moguće je zatražiti najviše {MaxInternalByIdsCount} proizvoda odjednom."));
        }

        var products = await _productRepository.GetByIdsWithCategoryAsync(ids, ct);
        return Result<List<ProductInternalResponse>>.Success(products.Select(ToInternalResponse).ToList());
    }

    /// <summary>
    /// Upper bound on <see cref="GetUpcomingAsync"/>'s <c>count</c>. This is the internal endpoint
    /// itself — not just its Ticketing caller — that must refuse a negative or absurdly large
    /// value before it reaches <see cref="IProductRepository.GetUpcomingAsync"/>'s
    /// <c>.Take(count)</c>, which SQL Server rejects outright for a negative value. Duplicated
    /// rather than shared with Ticketing's identical cap, same reasoning as MaxInternalByIdsCount:
    /// relying on the caller alone having validated is fragile against a future caller or a
    /// regression on their side.
    /// </summary>
    private const int MaxUpcomingCount = 50;

    public async Task<Result<List<ProductInternalResponse>>> GetUpcomingAsync(
        Guid? organizationId, int count, CancellationToken ct = default)
    {
        if (count <= 0 || count > MaxUpcomingCount)
        {
            return Result<List<ProductInternalResponse>>.Failure(Error.Validation(
                "product.invalid_count", $"Broj proizvoda mora biti između 1 i {MaxUpcomingCount}."));
        }

        var products = await _productRepository.GetUpcomingAsync(organizationId, DateTime.UtcNow, count, ct);
        return Result<List<ProductInternalResponse>>.Success(products.Select(ToInternalResponse).ToList());
    }

    public async Task<Result<List<OrganizationProductStatsResponse>>> GetOrganizationStatsAsync(CancellationToken ct = default)
    {
        var stats = await _productRepository.GetOrganizationStatsAsync(ct);
        return Result<List<OrganizationProductStatsResponse>>.Success(stats
            .Select(s => new OrganizationProductStatsResponse(
                s.OrganizationId, s.Total, s.Published, s.Draft, s.WithoutImage))
            .ToList());
    }

    private static ProductInternalResponse ToInternalResponse(Product product) =>
        new(product.Id, product.OrganizationId, product.Status, product.Category!.TicketingMode,
            product.Name, product.Date, product.City);

    /// <summary>Shared by PreviewAsync/CreateAsync/UpdateAsync so preview and the real write path
    /// always agree on the same validation message (per SPRINT_2 US-2.2's acceptance criteria).
    /// Field-level rules (Name/Description length etc.) already ran via CreateProductRequestValidator
    /// at the endpoint filter — this covers the cross-entity rules that need a DB lookup: the
    /// category must exist, and Date is required-and-future only when that category's
    /// TicketingMode is SingleOccurrence, must be null otherwise.</summary>
    private async Task<Result<Category>> ValidateAsync(UpsertProductRequest request, CancellationToken ct)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, ct);
        if (category is null)
            return Result<Category>.Failure(Error.Validation("product.category_not_found", "Odabrana kategorija ne postoji."));

        if (category.TicketingMode == TicketingMode.SingleOccurrence)
        {
            if (request.Date is null || request.Date <= DateTime.UtcNow)
                return Result<Category>.Failure(Error.Validation("product.date_required", "Datum je obavezan i mora biti u budućnosti."));
        }
        else if (request.Date is not null)
        {
            return Result<Category>.Failure(Error.Validation("product.date_not_applicable", "Datum se ne unosi za ovu vrstu kategorije."));
        }

        return Result<Category>.Success(category);
    }

    /// <summary>PlatformStaff bypasses ownership entirely; everyone else must own the product's organization.</summary>
    private static Error? AuthorizeOwnership(ClaimsPrincipal user, Guid organizationId)
    {
        if (user.IsPlatformStaff())
            return null;

        if (user.GetOrganizationId() != organizationId)
            return Error.Unauthorized("product.forbidden", "Proizvod ne pripada vašoj organizaciji.");

        return null;
    }

    private static string ContentTypeFor(string extension) => extension == "png" ? "image/png" : "image/jpeg";

    private Result<PagedResult<ProductResponse>> ToPagedResult(PagedResult<Product> paged) =>
        Result<PagedResult<ProductResponse>>.Success(new PagedResult<ProductResponse>
        {
            Items = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize,
        });

    // ImageUrls are derived from Azure Blob Storage (never stored columns) — same reasoning as
    // CategoryService.ToResponse/BuildIconUrl, generalized to a list ordered by DisplayOrder. The
    // derivation itself lives in ProductResponseFactory so RecommendationService renders identical
    // product cards from the same code.
    private ProductResponse ToResponse(Product product) => _responseFactory.ToResponse(product);
}
