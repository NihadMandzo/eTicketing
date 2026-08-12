using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;

namespace eTicketing.Catalog.Business.Categories;

public interface ICategoryService
{
    Task<Result<PagedResult<CategoryResponse>>> GetAsync(CategoryQuery query, CancellationToken ct = default);
    Task<Result<CategoryResponse>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<CategoryIcon>> GetIconAsync(int id, CancellationToken ct = default);
    Task<Result<CategoryResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<Result<CategoryResponse>> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}
