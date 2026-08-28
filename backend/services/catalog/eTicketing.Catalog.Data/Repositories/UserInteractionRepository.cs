using eTicketing.Catalog.Data.Entities;
using eTicketing.Contracts.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Catalog.Data.Repositories;

public class UserInteractionRepository : Repository<UserInteraction, Guid>, IUserInteractionRepository
{
    public UserInteractionRepository(CatalogDbContext context) : base(context) { }

    public async Task UpsertAsync(Guid userId, Guid productId, InteractionType type, DateTime occurredAt, CancellationToken ct = default)
    {
        // Tracked on purpose (no AsNoTracking): the increment below is a write, and the same
        // instance is what SaveChangesAsync must see as modified.
        var existing = await Query()
            .FirstOrDefaultAsync(i => i.UserId == userId && i.ProductId == productId && i.Type == type, ct);

        if (existing is not null)
        {
            existing.Count++;
            existing.LastOccurredAt = occurredAt;
            return;
        }

        await AddAsync(new UserInteraction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductId = productId,
            Type = type,
            Count = 1,
            LastOccurredAt = occurredAt,
        }, ct);
    }

    public void DiscardPendingInsert()
    {
        // EF leaves a rejected INSERT sitting in the Added state, so a caller that retries its
        // SaveChangesAsync without this would re-issue the very statement the unique index just
        // refused, forever.
        foreach (var entry in Context.ChangeTracker.Entries<UserInteraction>()
                     .Where(e => e.State == EntityState.Added))
        {
            entry.State = EntityState.Detached;
        }
    }

    public Task<List<InteractionTrainingRow>> GetTrainingRowsAsync(CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Select(i => new InteractionTrainingRow(
                i.UserId,
                i.ProductId,
                i.Type == InteractionType.Purchase
                    ? i.Count * UserInteraction.PurchaseWeightMultiplier
                    : i.Count))
            .ToListAsync(ct);

    public Task<List<UserInteraction>> GetByUserAsync(Guid userId, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            // Category as well as Product: the content-based ranking scores candidates by how
            // much of the user's history shares their category and TicketingMode, which lives on
            // Category rather than on Product itself.
            .Include(i => i.Product!).ThenInclude(p => p.Category)
            .Where(i => i.UserId == userId)
            .ToListAsync(ct);

    public Task<List<Guid>> GetPopularProductIdsAsync(City? city, int take, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Where(i => i.Type == InteractionType.Purchase)
            .Where(i => i.Product!.Status == PublishStatus.Published)
            .Where(i => city == null || i.Product!.City == city)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Buyers = g.Count() })
            .OrderByDescending(x => x.Buyers)
            .Take(take)
            .Select(x => x.ProductId)
            .ToListAsync(ct);

    public Task<List<Guid>> GetCoInteractedProductIdsAsync(Guid productId, int take, CancellationToken ct = default)
    {
        // Everyone who has touched the anchor product, in any way. Left as an IQueryable rather
        // than materialized so this stays one round trip with a subquery.
        var userIds = Query()
            .Where(i => i.ProductId == productId)
            .Select(i => i.UserId);

        return Query()
            .AsNoTracking()
            .Where(i => i.ProductId != productId && userIds.Contains(i.UserId))
            .Where(i => i.Product!.Status == PublishStatus.Published)
            .GroupBy(i => i.ProductId)
            // Distinct users, not row count: one person with both a View and a Purchase row for
            // the same product is one person's worth of evidence, not two.
            .Select(g => new { ProductId = g.Key, SharedUsers = g.Select(x => x.UserId).Distinct().Count() })
            .OrderByDescending(x => x.SharedUsers)
            .Take(take)
            .Select(x => x.ProductId)
            .ToListAsync(ct);
    }

    public async Task<InteractionStats> GetStatsAsync(CancellationToken ct = default)
    {
        var query = Query().AsNoTracking();

        return new InteractionStats(
            TotalInteractions: await query.CountAsync(ct),
            ViewCount: await query.CountAsync(i => i.Type == InteractionType.View, ct),
            PurchaseCount: await query.CountAsync(i => i.Type == InteractionType.Purchase, ct),
            DistinctUsers: await query.Select(i => i.UserId).Distinct().CountAsync(ct),
            DistinctProducts: await query.Select(i => i.ProductId).Distinct().CountAsync(ct));
    }
}
