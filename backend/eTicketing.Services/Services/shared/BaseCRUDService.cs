using AutoMapper;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Database.Entities;
using eTicketing.Services.Interfaces.Shared;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Services.Services.Shared;

public abstract class BaseCRUDService<TEntity, TResponse, TSearch, TRequest, TUpdateRequest> 
    : BaseService<TEntity, TResponse, TSearch>, ICRUDService<TResponse, TSearch, TRequest, TUpdateRequest>
    where TEntity : BaseEntity, new()
    where TSearch : BaseSearchObject
{
    protected BaseCRUDService(DbContext context, IMapper mapper) : base(context, mapper)
    {
    }

    public virtual async Task<TResponse> CreateAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new TEntity();
        
        Mapper.Map(request, entity);
        
        await BeforeCreateAsync(entity, request, cancellationToken);
        
        Context.Set<TEntity>().Add(entity);
        await Context.SaveChangesAsync(cancellationToken);
        
        await AfterCreateAsync(entity, request, cancellationToken);
        
        return MapToResponse(entity);
    }

    public virtual async Task<TResponse> UpdateAsync(int id, TUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await Context.Set<TEntity>().FindAsync(new object[] { id }, cancellationToken);
        
        if (entity == null)
            throw new KeyNotFoundException($"Entity with id {id} not found");
        
        Mapper.Map(request, entity);
        entity.UpdatedAt = DateTime.UtcNow;
        
        await BeforeUpdateAsync(entity, request, cancellationToken);
        
        Context.Set<TEntity>().Update(entity);
        await Context.SaveChangesAsync(cancellationToken);
        
        await AfterUpdateAsync(entity, request, cancellationToken);
        
        return MapToResponse(entity);
    }

    public virtual async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await Context.Set<TEntity>().FindAsync(new object[] { id }, cancellationToken);
        
        if (entity == null)
            return false;
        
        Context.Set<TEntity>().Remove(entity);
        await Context.SaveChangesAsync(cancellationToken);
        
        return true;
    }

    protected virtual Task BeforeCreateAsync(TEntity entity, TRequest request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual Task AfterCreateAsync(TEntity entity, TRequest request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual Task BeforeUpdateAsync(TEntity entity, TUpdateRequest request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual Task AfterUpdateAsync(TEntity entity, TUpdateRequest request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    protected virtual TResponse MapToResponse(TEntity entity)
    {
        return Mapper.Map<TResponse>(entity);
    }
}
