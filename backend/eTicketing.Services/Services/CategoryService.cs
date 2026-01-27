using AutoMapper;
using eTicketing.Model.Requests;
using eTicketing.Model.Responses;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Database;
using eTicketing.Services.Database.Entities;
using eTicketing.Services.Interfaces;
using eTicketing.Services.Services.Shared;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Services.Services;

public class CategoryService : BaseCRUDService<Category, CategoryResponse, BaseSearchObject, CategoryInsertRequest, CategoryUpdateRequest>, ICategoryService
{
    public CategoryService(eTicketingDbContext context, IMapper mapper) : base(context, mapper)
    {
    }

    protected override IQueryable<Category> ApplyFilter(IQueryable<Category> query, BaseSearchObject? search)
    {
        if (!string.IsNullOrWhiteSpace(search?.FTS))
        {
            query = query.Where(x => x.Name.Contains(search.FTS) || (x.Description != null && x.Description.Contains(search.FTS)));
        }

        query = query.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name);

        return query;
    }

    protected override async Task BeforeCreateAsync(Category entity, CategoryInsertRequest request, CancellationToken cancellationToken)
    {
        await ValidateUniqueCategoryName(entity.Name, null, cancellationToken);
        entity.CreatedAt = DateTime.UtcNow;
    }

    protected override Task AfterCreateAsync(Category entity, CategoryInsertRequest request, CancellationToken cancellationToken)
    {
        // Add any post-creation logic here if needed
        return Task.CompletedTask;
    }

    protected override async Task BeforeUpdateAsync(Category entity, CategoryUpdateRequest request, CancellationToken cancellationToken)
    {
        await ValidateUniqueCategoryName(entity.Name, entity.Id, cancellationToken);
        entity.UpdatedAt = DateTime.UtcNow;
    }

    protected override Task AfterUpdateAsync(Category entity, CategoryUpdateRequest request, CancellationToken cancellationToken)
    {
        // Add any post-update logic here if needed
        return Task.CompletedTask;
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
