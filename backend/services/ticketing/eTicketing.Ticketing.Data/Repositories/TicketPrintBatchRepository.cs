using eTicketing.Contracts.Persistence;
using eTicketing.Ticketing.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace eTicketing.Ticketing.Data.Repositories;

public class TicketPrintBatchRepository : Repository<TicketPrintBatch, Guid>, ITicketPrintBatchRepository
{
    private static readonly TicketPrintBatchStatus[] InFlightStatuses =
        [TicketPrintBatchStatus.Queued, TicketPrintBatchStatus.Rendering];

    private readonly TicketingDbContext _context;

    public TicketPrintBatchRepository(TicketingDbContext context) : base(context)
    {
        _context = context;
    }

    public Task<TicketPrintBatch?> GetHeaderAsync(Guid id, CancellationToken ct = default)
        => Query().AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);

    /// <summary>Caps at 50 — this backs a badge, not a report. An organizer can now clear any row
    /// on demand (DismissedAt), so the set no longer grows without a way to shrink it, but the cap
    /// stays as a backstop for someone who simply never clears anything.</summary>
    private const int MaxOutstandingPerOrganization = 50;

    public Task<List<TicketPrintBatch>> GetOutstandingForOrganizationAsync(Guid organizationId, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Where(b => b.OrganizationId == organizationId)
            // A collected batch is done with — the file is gone and the organizer has it. Only
            // work still worth surfacing stays in the badge.
            .Where(b => b.DownloadedAt == null)
            // Explicitly cleared by the organizer. Same idea as DownloadedAt above: the row still
            // exists as the record of the print run, it just no longer wants attention.
            .Where(b => b.DismissedAt == null)
            .OrderByDescending(b => b.CreatedAt)
            .Take(MaxOutstandingPerOrganization)
            .ToListAsync(ct);

    public Task<TicketPrintBatch?> GetLatestForProductAsync(Guid productId, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Where(b => b.ProductId == productId)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<bool> HasInFlightForProductAsync(Guid productId, CancellationToken ct = default)
        => Query().AnyAsync(b => b.ProductId == productId && InFlightStatuses.Contains(b.Status), ct);

    public Task<List<Guid>> GetUnfinishedIdsAsync(CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Where(b => InFlightStatuses.Contains(b.Status))
            .OrderBy(b => b.CreatedAt)
            .Select(b => b.Id)
            .ToListAsync(ct);

    public Task<List<Guid>> GetStaleReadyIdsAsync(DateTime completedBefore, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Where(b => b.Status == TicketPrintBatchStatus.Ready
                        && b.DownloadedAt == null
                        && b.CompletedAt != null
                        && b.CompletedAt < completedBefore)
            .Select(b => b.Id)
            .ToListAsync(ct);

    public Task<TicketPrintBatchFile?> GetFileRowAsync(Guid batchId, CancellationToken ct = default)
        => _context.Set<TicketPrintBatchFile>().FirstOrDefaultAsync(f => f.BatchId == batchId, ct);

    public void AddFile(TicketPrintBatchFile file) => _context.Set<TicketPrintBatchFile>().Add(file);

    public void RemoveFile(TicketPrintBatchFile file) => _context.Set<TicketPrintBatchFile>().Remove(file);
}
