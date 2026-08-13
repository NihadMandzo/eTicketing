using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Categories;

public interface ICategoryService
{
    Task<Result<PagedResult<CategoryResponse>>> GetAsync(CategoryQuery query, CancellationToken ct = default);
    Task<Result<CategoryResponse>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<CategoryResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<Result<CategoryResponse>> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>First-time icon upload — fails with a Conflict if the category already has one (use ReplaceIconAsync instead).</summary>
    Task<Result<CategoryResponse>> UploadIconAsync(int id, IFormFile icon, CancellationToken ct = default);

    /// <summary>Replaces an existing icon in place (same blob key) — fails with NotFound if the category has no icon yet (use UploadIconAsync instead).</summary>
    Task<Result<CategoryResponse>> ReplaceIconAsync(int id, IFormFile icon, CancellationToken ct = default);
}
