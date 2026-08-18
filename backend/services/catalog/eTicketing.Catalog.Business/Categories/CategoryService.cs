using eTicketing.Catalog.Data.Entities;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Shared.Storage;
using Mapster;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Categories;

public class CategoryService : ICategoryService
{
    private const string ContainerName = "category-icons";

    private readonly ICategoryRepository _categoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobStorageService _blobStorageService;

    public CategoryService(
        ICategoryRepository categoryRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        IBlobStorageService blobStorageService)
    {
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _blobStorageService = blobStorageService;
    }

    public async Task<Result<PagedResult<CategoryResponse>>> GetAsync(CategoryQuery query, CancellationToken ct = default)
    {
        var paged = await _categoryRepository.SearchAsync(query, ct);
        var items = paged.Items.Select(ToResponse).ToList();
        return Result<PagedResult<CategoryResponse>>.Success(new PagedResult<CategoryResponse>
        {
            Items = items,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize,
        });
    }

    public async Task<Result<CategoryResponse>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        return category is null
            ? Result<CategoryResponse>.Failure(Error.NotFound("category.not_found", "Kategorija nije pronađena."))
            : Result<CategoryResponse>.Success(ToResponse(category));
    }

    public async Task<Result<CategoryResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        var category = request.Adapt<Category>();

        await _categoryRepository.AddAsync(category, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CategoryResponse>.Success(ToResponse(category));
    }

    public async Task<Result<CategoryResponse>> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        if (category is null)
            return Result<CategoryResponse>.Failure(Error.NotFound("category.not_found", "Kategorija nije pronađena."));

        request.Adapt(category);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CategoryResponse>.Success(ToResponse(category));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure(Error.NotFound("category.not_found", "Kategorija nije pronađena."));

        // Deleting a category still referenced by products would otherwise hit the FK-restrict
        // constraint (ProductConfiguration) and surface as a raw DbUpdateException → 500. This is
        // an expected conflict, not a bug, so it's checked proactively and returned as a normal
        // domain Result instead.
        if (await _productRepository.ExistsForCategoryAsync(id, ct))
        {
            return Result.Failure(Error.Conflict(
                "category.in_use",
                "Kategorija se ne može obrisati jer je u upotrebi od strane jednog ili više proizvoda."));
        }

        _categoryRepository.Remove(category);
        await _unitOfWork.SaveChangesAsync(ct);

        // DB delete first: if SaveChangesAsync above throws (concurrency conflict, transient DB
        // error), the blob is left untouched rather than ending up orphaned while the category
        // row is still alive and pointing at it. If the blob delete below throws instead
        // (genuine Azure outage) — after the DB commit already succeeded — the category is gone
        // but the blob lingers; that orphaned-blob state is acceptable and recoverable (it's
        // simply never referenced again), unlike the reverse.
        if (category.IconBlobName is not null)
        {
            await _blobStorageService.DeleteAsync(ContainerName, category.IconBlobName, ct);
        }

        return Result.Success();
    }

    public async Task<Result<CategoryResponse>> UploadIconAsync(int id, IFormFile icon, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        if (category is null)
            return Result<CategoryResponse>.Failure(Error.NotFound("category.not_found", "Kategorija nije pronađena."));

        if (category.IconBlobName is not null)
        {
            return Result<CategoryResponse>.Failure(Error.Conflict(
                "category.icon_already_exists",
                "Kategorija već ima ikonu — koristite izmjenu da je zamijenite."));
        }

        var blobName = BlobNaming.BuildBlobName(category.Id, category.Name, extension: "png");
        await _blobStorageService.UploadAsync(ContainerName, blobName, icon.OpenReadStream(), "image/png", ct);
        category.IconBlobName = blobName;
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CategoryResponse>.Success(ToResponse(category));
    }

    public async Task<Result<CategoryResponse>> ReplaceIconAsync(int id, IFormFile icon, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        if (category is null)
            return Result<CategoryResponse>.Failure(Error.NotFound("category.not_found", "Kategorija nije pronađena."));

        if (category.IconBlobName is null)
        {
            return Result<CategoryResponse>.Failure(Error.NotFound(
                "category.icon_not_found",
                "Kategorija još nema ikonu — koristite kreiranje da je dodate."));
        }

        // Re-uploads to the SAME blob key (overwrite) — the key was fixed at first-upload time
        // and deliberately doesn't track later Name changes (see Category.IconBlobName).
        await _blobStorageService.UploadAsync(ContainerName, category.IconBlobName, icon.OpenReadStream(), "image/png", ct);

        return Result<CategoryResponse>.Success(ToResponse(category));
    }

    private CategoryResponse ToResponse(Category category) =>
        category.Adapt<CategoryResponse>() with { IconUrl = BuildIconUrl(category) };

    private string? BuildIconUrl(Category category) =>
        category.IconBlobName is null ? null : _blobStorageService.GetPublicUrl(ContainerName, category.IconBlobName);
}
