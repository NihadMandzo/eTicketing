using eTicketing.Payment.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Payment.Data.Repositories;

/// <summary>
/// Not a Repository&lt;T, TKey&gt;: that generic takes a Guid-ish key and offers CRUD this table has no
/// use for. A processed webhook event is only ever inserted and tested for existence.
/// </summary>
public class StripeEventRepository : IStripeEventRepository
{
    private readonly PaymentDbContext _context;

    public StripeEventRepository(PaymentDbContext context) => _context = context;

    public async Task AddAsync(StripeEvent stripeEvent, CancellationToken ct = default)
        => await _context.StripeEvents.AddAsync(stripeEvent, ct);

    public Task<bool> ExistsAsync(string stripeEventId, CancellationToken ct = default)
        => _context.StripeEvents.AnyAsync(e => e.Id == stripeEventId, ct);
}
