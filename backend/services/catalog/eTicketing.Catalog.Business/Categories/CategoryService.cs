using eTicketing.Catalog.Data.Entities;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using Mapster;
using Microsoft.Extensions.Options;

namespace eTicketing.Catalog.Business.Categories;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CatalogOptions _options;

    public CategoryService(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork, IOptions<CatalogOptions> options)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _options = options.Value;
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

    public async Task<Result<CategoryIcon>> GetIconAsync(int id, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        return category is null
            ? Result<CategoryIcon>.Failure(Error.NotFound("category.not_found", "Kategorija nije pronađena."))
            : Result<CategoryIcon>.Success(new CategoryIcon(category.IconData, category.IconContentType));
    }

    public async Task<Result<CategoryResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        var category = request.Adapt<Category>();
        (category.IconData, category.IconContentType) = await ReadIconAsync(request.Icon, ct);

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
        if (request.Icon is not null)
        {
            (category.IconData, category.IconContentType) = await ReadIconAsync(request.Icon, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CategoryResponse>.Success(ToResponse(category));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure(Error.NotFound("category.not_found", "Kategorija nije pronađena."));

        _categoryRepository.Remove(category);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static async Task<(byte[] Data, string ContentType)> ReadIconAsync(Microsoft.AspNetCore.Http.IFormFile icon, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        await icon.CopyToAsync(ms, ct);
        return (ms.ToArray(), icon.ContentType);
    }

    // IconUrl is derived (not a stored column), so it's filled in here rather than via Mapster —
    // same reasoning as OrganizationMappingConfig's UserCount, just done at the call site
    // instead of inside the mapping config since it needs CatalogOptions.
    private CategoryResponse ToResponse(Category category) =>
        category.Adapt<CategoryResponse>() with { IconUrl = BuildIconUrl(category.Id) };

    private string BuildIconUrl(int id) => $"{_options.PublicBaseUrl.TrimEnd('/')}/categories/{id}/icon";
}
