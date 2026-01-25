using AutoMapper;
using eTicketing.Model.Responses;
using eTicketing.Model.SearchObjects;
using eTicketing.Services.Database.Entities;
using eTicketing.Services.Interfaces.Shared;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Services.Services.Shared;

public abstract class BaseService<TEntity, TResponse, TSearch> : IService<TResponse, TSearch>
    where TEntity : BaseEntity
    where TSearch : BaseSearchObject
{
    protected readonly DbContext Context;
    protected readonly IMapper Mapper;

    protected BaseService(DbContext context, IMapper mapper)
    {
        Context = context;
        Mapper = mapper;
    }

    public virtual async Task<PagedResponse<TResponse>> GetAsync(TSearch? search = null, CancellationToken cancellationToken = default)
    {
        var query = Context.Set<TEntity>().AsQueryable();

        query = ApplyFilter(query, search);

        var totalCount = await query.CountAsync(cancellationToken);

        if (search?.Page.HasValue == true && search?.PageSize.HasValue == true)
        {
            query = query.Skip(search.Page.Value * search.PageSize.Value)
                         .Take(search.PageSize.Value);
        }

        var entities = await query.ToListAsync(cancellationToken);
        var items = Mapper.Map<List<TResponse>>(entities);

        return new PagedResponse<TResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = search?.Page,
            PageSize = search?.PageSize
        };
    }

    public virtual async Task<TResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await Context.Set<TEntity>().FindAsync(new object[] { id }, cancellationToken);
        
        if (entity == null)
            return default;

        return Mapper.Map<TResponse>(entity);
    }

    protected virtual IQueryable<TEntity> ApplyFilter(IQueryable<TEntity> query, TSearch? search)
    {
        return query;
    }
}
