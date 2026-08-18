using System.Security.Claims;
using eTicketing.Catalog.Business.Security;
using eTicketing.Catalog.Data.Entities;
using eTicketing.Catalog.Data.Repositories;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using Mapster;

namespace eTicketing.Catalog.Business.Products;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProductPreviewResponse>> PreviewAsync(UpsertProductRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(request, ct);
        if (validation.IsFailure)
            return Result<ProductPreviewResponse>.Failure(validation.Error);

        var category = validation.Value!;
        return Result<ProductPreviewResponse>.Success(new ProductPreviewResponse(
            request.Name, request.Description, request.Date, category.Id, category.Name, category.TicketingMode, request.ImageUrl));
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
            ImageUrl = request.ImageUrl,
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

        // A Published product stays Published after an edit — Status is deliberately untouched here.
        product.Name = request.Name;
        product.Description = request.Description;
        product.Date = request.Date;
        product.CategoryId = request.CategoryId;
        product.ImageUrl = request.ImageUrl;
        product.Category = validation.Value;

        await _unitOfWork.SaveChangesAsync(ct);

        return Result<ProductResponse>.Success(ToResponse(product));
    }

    public async Task<Result> DeleteAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdAsync(id, ct);
        if (product is null)
            return Result.Failure(Error.NotFound("product.not_found", "Proizvod nije pronađen."));

        var ownershipError = AuthorizeOwnership(user, product.OrganizationId);
        if (ownershipError is not null)
            return Result.Failure(ownershipError);

        _productRepository.Remove(product);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<PagedResult<ProductResponse>>> GetPublishedAsync(ProductQuery query, CancellationToken ct = default)
    {
        var paged = await _productRepository.SearchAsync(query, query.OrganizationId, query.CategoryId, PublishStatus.Published, ct);
        return ToPagedResult(paged);
    }

    public async Task<Result<PagedResult<ProductResponse>>> GetMineAsync(ProductQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var organizationId = user.GetOrganizationId();
        if (organizationId is null)
            return Result<PagedResult<ProductResponse>>.Failure(Error.Unauthorized("product.no_organization", "Nalog nije vezan za organizaciju."));

        var paged = await _productRepository.SearchAsync(query, organizationId, query.CategoryId, query.Status, ct);
        return ToPagedResult(paged);
    }

    public async Task<Result<PagedResult<ProductResponse>>> GetAllAsync(ProductQuery query, CancellationToken ct = default)
    {
        var paged = await _productRepository.SearchAsync(query, query.OrganizationId, query.CategoryId, query.Status, ct);
        return ToPagedResult(paged);
    }

    public async Task<Result<List<Guid>>> GetOrganizationIdsAsync(IReadOnlyList<int> categoryIds, CancellationToken ct = default)
    {
        var ids = await _productRepository.GetOrganizationIdsByCategoryIdsAsync(categoryIds, ct);
        return Result<List<Guid>>.Success(ids);
    }

    public async Task<Result<ProductInternalResponse>> GetInternalAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _productRepository.GetByIdWithCategoryAsync(id, ct);
        if (product is null)
            return Result<ProductInternalResponse>.Failure(Error.NotFound("product.not_found", "Proizvod nije pronađen."));

        return Result<ProductInternalResponse>.Success(new ProductInternalResponse(
            product.Id, product.OrganizationId, product.Status, product.Category!.TicketingMode));
    }

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

    private static Result<PagedResult<ProductResponse>> ToPagedResult(PagedResult<Product> paged) =>
        Result<PagedResult<ProductResponse>>.Success(new PagedResult<ProductResponse>
        {
            Items = paged.Items.Select(ToResponse).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize,
        });

    private static ProductResponse ToResponse(Product product) => product.Adapt<ProductResponse>();
}
