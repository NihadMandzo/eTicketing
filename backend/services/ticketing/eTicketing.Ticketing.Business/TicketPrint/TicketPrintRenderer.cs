using eTicketing.Contracts.Persistence;
using eTicketing.Shared.TicketPdf;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace eTicketing.Ticketing.Business.TicketPrint;

/// <summary>Renders one queued batch to a PDF and records the outcome. Driven by
/// TicketPrintRenderWorker in .Api — kept here, in Business, because it is domain work rather than
/// hosting plumbing, and because that makes it testable without a host.</summary>
public interface ITicketPrintRenderer
{
    /// <summary>Renders the batch and moves it to Ready, or to Failed with a Bosnian reason. Never
    /// throws for an expected failure: the worker must not be able to die on one bad batch, and the
    /// export screen must never be left waiting on a spinner that will not end.</summary>
    Task RenderAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>Puts every batch left mid-render by a restart back in the queue, and returns the
    /// ids so the caller can nudge them. Also picks up anything still Queued.</summary>
    Task<List<Guid>> RecoverUnfinishedAsync(CancellationToken ct = default);

    /// <summary>Destroys stored files nobody ever collected. See TicketPrintBatchFile's remarks for
    /// why they must not linger.</summary>
    Task<int> SweepStaleFilesAsync(TimeSpan retention, CancellationToken ct = default);
}

public class TicketPrintRenderer : ITicketPrintRenderer
{
    /// <summary>How many tickets are pulled out of the database at a time. Large enough that a
    /// 5000-ticket batch is a handful of round-trips, small enough that no single query
    /// materialises the whole batch.</summary>
    private const int PageSize = 500;

    /// <summary>Diagnostic hook, not production surface — see AssemblyInfo.cs.
    /// TicketPrintRendererTests asserts this differs from the thread that called RenderAsync, which
    /// is the actual guarantee TaskCreationOptions.LongRunning below exists to provide: GeneratePdf's
    /// long CPU-bound work must never run on a thread-pool worker Kestrel needs for other
    /// requests.</summary>
    internal int? LastGeneratePdfThreadId { get; private set; }

    private readonly ITicketPrintBatchRepository _batchRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ICatalogClient _catalogClient;
    private readonly TicketQrCodec _qrCodec;
    private readonly TicketSupportInfo _support;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PlatformClock _clock;
    private readonly ILogger<TicketPrintRenderer> _logger;

    public TicketPrintRenderer(
        ITicketPrintBatchRepository batchRepository,
        ITicketRepository ticketRepository,
        ICatalogClient catalogClient,
        TicketQrCodec qrCodec,
        IOptions<TicketSupportOptions> support,
        IUnitOfWork unitOfWork,
        PlatformClock clock,
        ILogger<TicketPrintRenderer> logger)
    {
        _batchRepository = batchRepository;
        _ticketRepository = ticketRepository;
        _catalogClient = catalogClient;
        _qrCodec = qrCodec;
        _support = new TicketSupportInfo(support.Value.Email, support.Value.Phone);
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task RenderAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, ct);
        if (batch is null)
        {
            _logger.LogWarning("Izvoz {BatchId} više ne postoji — preskačem.", batchId);
            return;
        }

        if (batch.Status is TicketPrintBatchStatus.Ready or TicketPrintBatchStatus.Failed)
        {
            // Already handled — a duplicate nudge (startup recovery racing a fresh enqueue) must
            // not re-render a batch and hand the organizer a second, different file.
            return;
        }

        batch.Status = TicketPrintBatchStatus.Rendering;
        batch.RenderedCount = 0;
        _batchRepository.Update(batch);
        await _unitOfWork.SaveChangesAsync(ct);

        try
        {
            var product = await _catalogClient.GetProductAsync(batch.ProductId, ct);
            if (product is null)
            {
                await FailAsync(batch, "Proizvod za ovaj izvoz više ne postoji.", ct);
                return;
            }

            var tickets = new List<PrintTicketModel>(batch.TicketCount);
            for (var skip = 0; skip < batch.TicketCount; skip += PageSize)
            {
                var page = await _ticketRepository.GetForPrintBatchAsync(batch.Id, skip, PageSize, ct);
                if (page.Count == 0) break;

                tickets.AddRange(page.Select(ToPrintModel));

                batch.RenderedCount = tickets.Count;
                _batchRepository.Update(batch);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            if (tickets.Count == 0)
            {
                await FailAsync(batch, "Izvoz nema nijednu ulaznicu za štampu.", ct);
                return;
            }

            var sheet = new PrintSheetModel(
                product.Name,
                product.City.ToDisplayName(),
                product.Date,
                product.TicketingMode,
                batch.CreatedAt,
                _support,
                tickets);

            var document = new PrintSheetDocument(sheet);
            // A 900-page batch is tens of seconds of pure CPU work inside QuestPDF's layout engine.
            // Calling GeneratePdf() inline would run that on whichever thread-pool worker resumed
            // this async method — the same shared pool Kestrel uses to service every other request
            // this instance handles. The pool does not grow fast enough under a sudden multi-second
            // block to absorb that, so an organizer submitting one huge export was starving every
            // other call into this service (including a buyer's own purchase) until the render
            // finished. TaskCreationOptions.LongRunning asks the runtime for a dedicated thread
            // outside the pool's worker-count accounting, so this stays CPU-heavy without taking a
            // pool thread hostage for the duration.
            var content = await Task.Factory.StartNew(
                () =>
                {
                    LastGeneratePdfThreadId = Environment.CurrentManagedThreadId;
                    return document.GeneratePdf();
                },
                ct,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            _batchRepository.AddFile(new TicketPrintBatchFile { BatchId = batch.Id, Content = content });

            batch.Status = TicketPrintBatchStatus.Ready;
            batch.RenderedCount = tickets.Count;
            batch.PageCount = document.PageCount;
            batch.FileSizeBytes = content.LongLength;
            batch.CompletedAt = _clock.UtcNow;
            batch.ErrorMessage = null;
            _batchRepository.Update(batch);
            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Izvoz {BatchId} je spreman: {Count} ulaznica na {Pages} stranica ({Size} KB).",
                batch.Id, tickets.Count, document.PageCount, content.LongLength / 1024);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host shutting down. Leave the batch in Rendering — startup recovery re-queues it, and
            // marking it Failed here would ask the organizer to retry work that was never wrong.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generisanje PDF-a za izvoz {BatchId} nije uspjelo.", batch.Id);
            await FailAsync(batch, "Generisanje PDF-a nije uspjelo. Pokušajte ponovo.", ct);
        }
    }

    public async Task<List<Guid>> RecoverUnfinishedAsync(CancellationToken ct = default)
    {
        var ids = await _batchRepository.GetUnfinishedIdsAsync(ct);
        if (ids.Count == 0) return ids;

        // A batch left in Rendering belonged to a process that is gone. Put it back to Queued so
        // the state the organizer sees matches reality while it waits its turn again.
        foreach (var id in ids)
        {
            var batch = await _batchRepository.GetByIdAsync(id, ct);
            if (batch is null || batch.Status != TicketPrintBatchStatus.Rendering) continue;

            batch.Status = TicketPrintBatchStatus.Queued;
            batch.RenderedCount = 0;
            _batchRepository.Update(batch);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Nastavljam {Count} nedovršenih izvoza ulaznica nakon pokretanja servisa.", ids.Count);
        return ids;
    }

    public async Task<int> SweepStaleFilesAsync(TimeSpan retention, CancellationToken ct = default)
    {
        var cutoff = _clock.UtcNow - retention;
        var ids = await _batchRepository.GetStaleReadyIdsAsync(cutoff, ct);
        if (ids.Count == 0) return 0;

        var swept = 0;
        foreach (var id in ids)
        {
            var file = await _batchRepository.GetFileRowAsync(id, ct);
            if (file is null) continue;

            _batchRepository.RemoveFile(file);
            swept++;
        }

        if (swept > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Obrisano {Count} nepreuzetih PDF-ova izvoza starijih od {Days} dana.", swept, retention.TotalDays);
        }

        return swept;
    }

    private async Task FailAsync(TicketPrintBatch batch, string reason, CancellationToken ct)
    {
        batch.Status = TicketPrintBatchStatus.Failed;
        batch.ErrorMessage = reason;
        batch.CompletedAt = _clock.UtcNow;
        _batchRepository.Update(batch);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private PrintTicketModel ToPrintModel(Ticket ticket) => new(
        ticket.Id,
        // Every ticket in a print batch is minted with a serial; the fallback only exists so a
        // malformed row cannot take the whole render down.
        ticket.SerialNumber ?? 0,
        _qrCodec.Sign(ticket.Id),
        ticket.Sector?.Name ?? string.Empty,
        ticket.TicketType?.Name,
        ticket.PricePaid,
        ticket.ValidDate);
}
