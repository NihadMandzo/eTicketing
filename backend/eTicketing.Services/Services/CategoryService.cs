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

    public override async Task<CategoryResponse> CreateAsync(CategoryInsertRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Category();
        Mapper.Map(request, entity);

        await ValidateUniqueCategoryName(entity.Name, null, cancellationToken);

        // Icon is required — upload to blob storage
        var iconUrl = await _blobStorageService.UploadAsync(request.Icon, CategoryIconsContainer);
        entity.Image = new Image { ImageUrl = iconUrl };
        entity.CreatedAt = DateTime.UtcNow;

        try
        {
            Context.Set<Category>().Add(entity);
            await Context.SaveChangesAsync(cancellationToken);
            return MapToResponse(entity);
        }
        catch
        {
            if (iconUrl != null)
            {
                await _blobStorageService.DeleteAsync(iconUrl, CategoryIconsContainer);
            }
            throw;
        }
    }

    public override async Task<CategoryResponse> UpdateAsync(int id, CategoryUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await Context.Set<Category>().FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
            throw new KeyNotFoundException($"Entitet sa ID-om {id} nije pronađen");

        Mapper.Map(request, entity);
        entity.UpdatedAt = DateTime.UtcNow;

        await ValidateUniqueCategoryName(entity.Name, entity.Id, cancellationToken);

        // Load existing image
        await Context.Entry(entity)
            .Reference(e => e.Image)
            .LoadAsync(cancellationToken);

        string? oldIconUrl = null;
        string? newIconUrl = null;

        // Handle new icon upload
        if (request.Icon != null)
        {
            oldIconUrl = entity.Image?.ImageUrl;
            newIconUrl = await _blobStorageService.UploadAsync(request.Icon, CategoryIconsContainer);

            if (entity.Image != null)
            {
                entity.Image.ImageUrl = newIconUrl;
            }
            else
            {
                entity.Image = new Image { ImageUrl = newIconUrl };
            }
        }

        try
        {
            Context.Set<Category>().Update(entity);
            await Context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (newIconUrl != null)
            {
                await _blobStorageService.DeleteAsync(newIconUrl, CategoryIconsContainer);
            }
            throw;
        }

        // Delete old blob after successful DB commit
        if (oldIconUrl != null && newIconUrl != null)
        {
            try
            {
                await _blobStorageService.DeleteAsync(oldIconUrl, CategoryIconsContainer);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old icon blob {BlobUrl} for category {CategoryId}. Blob may be orphaned.",
                    oldIconUrl, entity.Id);
            }
        }

        return MapToResponse(entity);
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
