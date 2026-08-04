using Microsoft.EntityFrameworkCore;

namespace eTicketing.Contracts.Persistence;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly DbContext Context;
    protected readonly DbSet<T> DbSet;

    public Repository(DbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => DbSet.FindAsync([id], ct).AsTask();

    public IQueryable<T> Query() => DbSet.AsQueryable();

    public Task AddAsync(T entity, CancellationToken ct = default)
        => DbSet.AddAsync(entity, ct).AsTask();

    public void Update(T entity) => DbSet.Update(entity);

    public void Remove(T entity) => DbSet.Remove(entity);
}
