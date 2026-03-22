using AutoMapper;
using eTicketing.Model.Requests;
using eTicketing.Model.Responses;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Database;
using eTicketing.Services.Database.Entities;
using eTicketing.Services.Interfaces;
using eTicketing.Services.Services.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eTicketing.Services.Services;

public class CategoryService : BaseCRUDService<Category, CategoryResponse, BaseSearchObject, CategoryInsertRequest, CategoryUpdateRequest>, ICategoryService
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<CategoryService> _logger;
    private const string CategoryIconsContainer = "category-icons";

    public CategoryService(
        eTicketingDbContext context,
        IMapper mapper,
        IBlobStorageService blobStorageService,
        ILogger<CategoryService> logger) : base(context, mapper)
    {
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    protected override IQueryable<Category> ApplyFilter(IQueryable<Category> query, BaseSearchObject? search)
    {
        if (!string.IsNullOrWhiteSpace(search?.FTS))
        {
            query = query.Where(x => x.Name.Contains(search.FTS) || (x.Description != null && x.Description.Contains(search.FTS)));
        }

        query = query.Include(x => x.Image);
        query = query.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);

        return query;
    }

    protected override async Task BeforeCreateAsync(Category entity, CategoryInsertRequest request, CancellationToken cancellationToken)
    {
        await ValidateUniqueCategoryName(entity.Name, null, cancellationToken);

        // Icon is required — upload to blob storage
        var iconUrl = await _blobStorageService.UploadAsync(request.Icon, CategoryIconsContainer);
        entity.Image = new Image { ImageUrl = iconUrl };

        entity.CreatedAt = DateTime.UtcNow;
    }

    protected override Task AfterCreateAsync(Category entity, CategoryInsertRequest request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    // Temporary field for tracking icon URL to delete after commit
    private string? _pendingIconDeleteUrl;

    protected override async Task BeforeUpdateAsync(Category entity, CategoryUpdateRequest request, CancellationToken cancellationToken)
    {
        await ValidateUniqueCategoryName(entity.Name, entity.Id, cancellationToken);

        // Load existing image
        await Context.Entry(entity)
            .Reference(e => e.Image)
            .LoadAsync(cancellationToken);

        // Handle new icon upload (optional — null means keep existing)
        if (request.Icon != null)
        {
            string? oldIconUrl = entity.Image?.ImageUrl;
            var newIconUrl = await _blobStorageService.UploadAsync(request.Icon, CategoryIconsContainer);

            if (entity.Image != null)
            {
                // Update existing image record
                entity.Image.ImageUrl = newIconUrl;
            }
            else
            {
                // Create new image record (e.g. category had no icon yet due to legacy data)
                entity.Image = new Image { ImageUrl = newIconUrl };
            }

            // Track old URL for cleanup after DB commit
            if (oldIconUrl != null)
            {
                _pendingIconDeleteUrl = oldIconUrl;
            }
        }

        entity.UpdatedAt = DateTime.UtcNow;
    }

    protected override async Task AfterUpdateAsync(Category entity, CategoryUpdateRequest request, CancellationToken cancellationToken)
    {
        // Delete old blob after successful DB commit
        if (_pendingIconDeleteUrl != null)
        {
            try
            {
                await _blobStorageService.DeleteAsync(_pendingIconDeleteUrl, CategoryIconsContainer);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old icon blob {BlobUrl} for category {CategoryId}. Blob may be orphaned.",
                    _pendingIconDeleteUrl, entity.Id);
            }
            finally
            {
                _pendingIconDeleteUrl = null;
            }
        }
    }

    public override async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await Context.Set<Category>()
            .Include(c => c.Image)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null)
            return false;

        // Capture icon URL before deletion
        string? iconUrl = category.Image?.ImageUrl;

        // Remove image record and FK
        if (category.Image != null)
        {
            Context.Set<Image>().Remove(category.Image);
            category.Image = null;
            category.ImageId = null;
        }

        Context.Set<Category>().Remove(category);
        await Context.SaveChangesAsync(cancellationToken);

        // Delete icon blob after successful DB commit
        if (iconUrl != null)
        {
            try
            {
                await _blobStorageService.DeleteAsync(iconUrl, CategoryIconsContainer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete icon blob {BlobUrl} for deleted category {CategoryId}. Blob may be orphaned.",
                    iconUrl, id);
            }
        }

        return true;
    }

    private async Task ValidateUniqueCategoryName(string name, int? excludeId, CancellationToken cancellationToken)
    {
        var query = Context.Set<Category>().Where(x => x.Name == name);
        
        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        var exists = await query.AnyAsync(cancellationToken);
        
        if (exists)
        {
            throw new InvalidOperationException($"Kategorija sa imenom '{name}' već postoji");
        }
    }
}
