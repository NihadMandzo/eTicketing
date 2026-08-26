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

    public Task<List<TicketPrintBatch>> GetOutstandingForOrganizationAsync(Guid organizationId, CancellationToken ct = default)
        => Query()
            .AsNoTracking()
            .Where(b => b.OrganizationId == organizationId)
            // A collected batch is done with — the file is gone and the organizer has it. Only
            // work still worth surfacing stays in the badge.
            .Where(b => b.DownloadedAt == null)
            .OrderByDescending(b => b.CreatedAt)
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

    public Task<byte[]?> GetFileAsync(Guid batchId, CancellationToken ct = default)
        => _context.Set<TicketPrintBatchFile>()
            .AsNoTracking()
            .Where(f => f.BatchId == batchId)
            .Select(f => f.Content)
            .FirstOrDefaultAsync(ct)!;

    public Task<TicketPrintBatchFile?> GetFileRowAsync(Guid batchId, CancellationToken ct = default)
        => _context.Set<TicketPrintBatchFile>().FirstOrDefaultAsync(f => f.BatchId == batchId, ct);

    public void AddFile(TicketPrintBatchFile file) => _context.Set<TicketPrintBatchFile>().Add(file);

    public void RemoveFile(TicketPrintBatchFile file) => _context.Set<TicketPrintBatchFile>().Remove(file);
}
