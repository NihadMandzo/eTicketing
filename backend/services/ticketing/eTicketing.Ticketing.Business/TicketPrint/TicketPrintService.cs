using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Shared.TicketPdf;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.TicketPrint;

/// <summary>
/// Issues physical tickets in bulk so an organizer can sell them over a counter.
///
/// The important thing to hold onto: <b>creating a batch is a sale, not a report.</b> Every row it
/// mints is a real, gate-valid <see cref="Ticket"/> carrying a real signed QR payload, and it draws
/// against the same Redis capacity counter a website purchase does. Printing 200 tickets for a
/// 200-seat sector therefore sells that sector out online too, which is the entire point — the two
/// channels must never be able to sell the same seat.
///
/// The PDF is an afterthought by comparison. It is rendered off the request thread by
/// TicketPrintRenderWorker because a batch can run to thousands of tickets, and it is destroyed the
/// moment the organizer downloads it (see <see cref="DownloadAsync"/>).
/// </summary>
public class TicketPrintService : ITicketPrintService
{
    /// <summary>Ceiling on a single batch. Bounds how long one render occupies the worker and how
    /// much memory the finished document needs; an organizer printing more than this can queue a
    /// second batch, and stub numbering continues seamlessly across the two.</summary>
    public const int MaxTicketsPerBatch = 5000;

    /// <summary>Printed tickets are permanent the instant they are minted, so the hold that claims
    /// their capacity is confirmed immediately. The TTL only has to outlive the confirm call
    /// itself — nothing waits on a shopper making up their mind here.</summary>
    private static readonly TimeSpan CapacityHoldTtl = TimeSpan.FromMinutes(2);

    private readonly ISectorRepository _sectorRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketPrintBatchRepository _batchRepository;
    private readonly ISectorCapacityLock _capacityLock;
    private readonly ICatalogClient _catalogClient;
    private readonly ITicketPrintQueue _queue;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PlatformClock _clock;
    private readonly ILogger<TicketPrintService> _logger;

    public TicketPrintService(
        ISectorRepository sectorRepository,
        ITicketRepository ticketRepository,
        ITicketPrintBatchRepository batchRepository,
        ISectorCapacityLock capacityLock,
        ICatalogClient catalogClient,
        ITicketPrintQueue queue,
        IUnitOfWork unitOfWork,
        PlatformClock clock,
        ILogger<TicketPrintService> logger)
    {
        _sectorRepository = sectorRepository;
        _ticketRepository = ticketRepository;
        _batchRepository = batchRepository;
        _capacityLock = capacityLock;
        _catalogClient = catalogClient;
        _queue = queue;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    // -------------------------------------------------------------------------------------------
    // Options
    // -------------------------------------------------------------------------------------------

    public async Task<Result<TicketPrintOptionsResponse>> GetOptionsAsync(
        Guid productId, DateOnly? date, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var access = await ResolveProductAsync(productId, user, ct);
        if (access.IsFailure) return Result<TicketPrintOptionsResponse>.Failure(access.Error);

        var product = access.Value!;

        var sectors = await _sectorRepository.GetPublishedByProductWithTicketTypesAsync(productId, ct);

        var blockedReason = BlockedReasonFor(product.TicketingMode);

        // DailyEntry capacity is tracked per (sector, calendar day), so "remaining" only has a
        // meaning once a day is chosen. Until then the counter to read is the sector's own, which
        // is what an untouched day would give anyway.
        var counterDate = product.TicketingMode == TicketingMode.DailyEntry ? date : null;

        // One Redis round-trip per sector, issued together rather than in series — a product with
        // twenty sectors was twenty sequential waits. Safe to parallelize precisely because
        // GetRemainingAsync is read-only: nothing here reserves capacity, so there is no ordering
        // to preserve and nothing to roll back. (The hold loop in CreateAsync is the opposite case
        // and stays sequential — see the comment there.) Copies
        // SectorService.GetPublishedAsync's own Task.WhenAll over the same call.
        var remaining = await Task.WhenAll(sectors.Select(s =>
            _capacityLock.GetRemainingAsync(s.Id, s.Capacity, counterDate, ct)));

        var options = sectors
            .Select((sector, index) => new TicketPrintSectorOption(
                sector.Id,
                sector.Name,
                sector.Capacity,
                remaining[index],
                sector.Price,
                sector.PeriodYear,
                sector.PeriodMonth,
                [.. sector.TicketTypes
                    .OrderBy(t => t.Name)
                    .Select(t => new TicketPrintTicketTypeOption(t.Id, t.Name, t.Price))]))
            .ToList();

        var nextSerial = await _ticketRepository.GetMaxSerialNumberAsync(productId, ct) + 1;

        return Result<TicketPrintOptionsResponse>.Success(new TicketPrintOptionsResponse(
            productId,
            product.Name,
            product.TicketingMode,
            product.Date,
            CanExport: blockedReason is null && sectors.Count > 0,
            BlockedReason: blockedReason ?? (sectors.Count == 0
                ? "Proizvod nema nijedan objavljen sektor. Dodajte i objavite sektor prije izvoza ulaznica."
                : null),
            NextSerialNumber: nextSerial,
            MaxTicketsPerBatch: MaxTicketsPerBatch,
            TicketsPerSheet: PrintSheetDocument.TicketsPerSheet,
            Sectors: options));
    }

    // -------------------------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------------------------

    public async Task<Result<TicketPrintBatchResponse>> CreateAsync(
        CreateTicketPrintBatchRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var access = await ResolveProductAsync(request.ProductId, user, ct);
        if (access.IsFailure) return Result<TicketPrintBatchResponse>.Failure(access.Error);

        var product = access.Value!;

        if (BlockedReasonFor(product.TicketingMode) is { } blocked)
        {
            return Result<TicketPrintBatchResponse>.Failure(
                Error.Validation("print.mode_not_supported", blocked));
        }

        // One at a time, so the export screen never has to reason about two overlapping jobs and
        // "your PDF is ready" is unambiguous about which PDF it means.
        if (await _batchRepository.HasInFlightForProductAsync(request.ProductId, ct))
        {
            return Result<TicketPrintBatchResponse>.Failure(Error.Conflict(
                "print.batch_in_progress",
                "Izvoz ulaznica za ovaj proizvod je već u toku. Sačekajte da se završi pa pokušajte ponovo."));
        }

        var dateCheck = ValidateValidDate(product.TicketingMode, request.ValidDate);
        if (dateCheck.IsFailure) return Result<TicketPrintBatchResponse>.Failure(dateCheck.Error);

        var sectorIds = request.Lines.Select(l => l.SectorId).Distinct().ToList();
        var sectors = await _sectorRepository.GetByIdsWithTicketTypesAsync(sectorIds, ct);

        var resolved = ResolveLines(request, product.TicketingMode, sectors);
        if (resolved.IsFailure) return Result<TicketPrintBatchResponse>.Failure(resolved.Error);

        var lines = resolved.Value!;
        var perSector = lines
            .GroupBy(l => l.Sector)
            .Select(g => (Sector: g.Key, Quantity: g.Sum(l => l.Quantity)))
            .ToList();

        // Claim every sector's capacity before minting anything. Each hold is atomic in Redis, so
        // two organizers (or an organizer and a website buyer) racing for the last seats cannot
        // both win — and if any sector comes up short, the ones already taken are handed straight
        // back rather than silently swallowing capacity for a batch that was never created.
        var holds = new List<string>(perSector.Count);
        foreach (var (sector, quantity) in perSector)
        {
            // ownerId: null — a system hold, not a buyer's. These counter tickets are minted for
            // the organizer to sell at the door, so there is no account to bind the hold to, and
            // the hold ids never leave this method: it releases or confirms every one of them
            // before returning, so nothing outside can present one.
            var hold = await _capacityLock.TryHoldAsync(
                sector.Id, sector.Capacity, quantity, request.ValidDate, CapacityHoldTtl, null, ct);

            if (!hold.Success)
            {
                foreach (var taken in holds) await _capacityLock.ReleaseAsync(taken, ct);

                var remaining = await _capacityLock.GetRemainingAsync(sector.Id, sector.Capacity, request.ValidDate, ct);
                return Result<TicketPrintBatchResponse>.Failure(Error.Conflict(
                    "print.capacity_exceeded",
                    $"Sektor \"{sector.Name}\" nema dovoljno slobodnih mjesta — traženo {quantity}, preostalo {remaining}."));
            }

            holds.Add(hold.HoldId!);
        }

        var batchId = Guid.NewGuid();
        var firstSerial = await _ticketRepository.GetMaxSerialNumberAsync(request.ProductId, ct) + 1;
        var serial = firstSerial;

        var tickets = new List<Ticket>(lines.Sum(l => l.Quantity));
        foreach (var line in lines)
        {
            for (var i = 0; i < line.Quantity; i++)
            {
                tickets.Add(Ticket.ForPrint(
                    line.Sector.Id,
                    line.TicketTypeId,
                    batchId,
                    request.ProductId,
                    line.UnitPrice,
                    serial++,
                    request.ValidDate));
            }
        }

        var batch = new TicketPrintBatch
        {
            Id = batchId,
            ProductId = request.ProductId,
            OrganizationId = product.OrganizationId,
            RequestedByUserId = user.GetUserId(),
            ProductName = product.Name,
            Status = TicketPrintBatchStatus.Queued,
            TicketCount = tickets.Count,
            SerialFrom = firstSerial,
            SerialTo = serial - 1,
            NominalValue = tickets.Sum(t => t.PricePaid),
            ValidDate = request.ValidDate,
        };

        await _batchRepository.AddAsync(batch, ct);
        foreach (var ticket in tickets) await _ticketRepository.AddAsync(ticket, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another request won the race: it passed the in-flight check and computed the same
            // MAX(SerialNumber)+1 before this one committed, and the unique (ProductId,
            // SerialNumber) index rejected the loser. Not a bug — hand the capacity back and let
            // the organizer retry, by which point the winner's batch is the one in flight.
            foreach (var taken in holds) await _capacityLock.ReleaseAsync(taken, ct);
            return Result<TicketPrintBatchResponse>.Failure(Error.Conflict(
                "print.concurrent_create",
                "Neko drugi je upravo pokrenuo izvoz ulaznica za ovaj proizvod. Pokušajte ponovo za trenutak."));
        }
        catch
        {
            // The capacity is claimed but the tickets never landed. Hand it back rather than
            // leaving a sector permanently short by a batch that does not exist.
            foreach (var taken in holds) await _capacityLock.ReleaseAsync(taken, ct);
            throw;
        }

        // Only now do the holds become permanent — after the tickets they represent are committed.
        foreach (var taken in holds) await _capacityLock.ConfirmAsync(taken, ct);

        _logger.LogInformation(
            "Izdato {Count} štampanih ulaznica za proizvod {ProductId} (serijski brojevi {From}-{To}), batch {BatchId}.",
            tickets.Count, request.ProductId, batch.SerialFrom, batch.SerialTo, batchId);

        _queue.Enqueue(batchId);

        return Result<TicketPrintBatchResponse>.Success(ToResponse(batch));
    }

    // -------------------------------------------------------------------------------------------
    // Read
    // -------------------------------------------------------------------------------------------

    public async Task<Result<TicketPrintBatchResponse>> GetAsync(Guid batchId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var batch = await _batchRepository.GetHeaderAsync(batchId, ct);
        if (batch is null)
        {
            return Result<TicketPrintBatchResponse>.Failure(
                Error.NotFound("print.batch_not_found", "Izvoz ulaznica nije pronađen."));
        }

        if (!CanAccess(batch, user))
        {
            return Result<TicketPrintBatchResponse>.Failure(
                Error.Unauthorized("print.forbidden", "Nemate pristup ovom izvozu ulaznica."));
        }

        return Result<TicketPrintBatchResponse>.Success(ToResponse(batch));
    }

    public async Task<Result<List<TicketPrintBatchResponse>>> GetOutstandingAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var organizationId = user.GetOrganizationId();
        if (organizationId is null)
        {
            // Platform staff have no organization of their own, so there is no "my exports" list to
            // show them. An empty list, not a failure — the badge simply stays quiet.
            return Result<List<TicketPrintBatchResponse>>.Success([]);
        }

        var batches = await _batchRepository.GetOutstandingForOrganizationAsync(organizationId.Value, ct);
        return Result<List<TicketPrintBatchResponse>>.Success([.. batches.Select(ToResponse)]);
    }

    public async Task<Result<TicketPrintBatchResponse?>> GetLatestForProductAsync(
        Guid productId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var access = await ResolveProductAsync(productId, user, ct);
        if (access.IsFailure) return Result<TicketPrintBatchResponse?>.Failure(access.Error);

        var batch = await _batchRepository.GetLatestForProductAsync(productId, ct);
        return Result<TicketPrintBatchResponse?>.Success(batch is null ? null : ToResponse(batch));
    }

    // -------------------------------------------------------------------------------------------
    // Download
    // -------------------------------------------------------------------------------------------

    public async Task<Result<TicketPrintDownload>> DownloadAsync(Guid batchId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, ct);
        if (batch is null)
        {
            return Result<TicketPrintDownload>.Failure(
                Error.NotFound("print.batch_not_found", "Izvoz ulaznica nije pronađen."));
        }

        // Organizers are deliberately denied a buyer's ticket PDF by TicketPdfService, on the
        // grounds that the sheet carries a working gate code and the organizer already has the
        // scanner. This is the narrow, intentional exception: they minted these tickets in order to
        // print and sell them, so withholding the codes would defeat the whole feature.
        if (!CanAccess(batch, user))
        {
            return Result<TicketPrintDownload>.Failure(
                Error.Unauthorized("print.forbidden", "Nemate pristup ovom izvozu ulaznica."));
        }

        if (batch.Status != TicketPrintBatchStatus.Ready)
        {
            return Result<TicketPrintDownload>.Failure(Error.Conflict(
                "print.not_ready",
                batch.Status == TicketPrintBatchStatus.Failed
                    ? "Izvoz nije uspio. Pokušajte ponovo."
                    : "PDF još nije spreman. Pokušajte za nekoliko trenutaka."));
        }

        var file = await _batchRepository.GetFileRowAsync(batchId, ct);
        if (file is null)
        {
            return Result<TicketPrintDownload>.Failure(Error.NotFound(
                "print.file_gone",
                "PDF ovog izvoza više nije dostupan. Ulaznice su i dalje važeće — napravite novi izvoz ako vam treba još primjeraka."));
        }

        var content = file.Content;

        // Handed over exactly once. The bytes are thousands of working gate codes, and this
        // platform never keeps a ticket PDF around longer than it must — the same reason
        // GET /tickets/{id}/pdf re-renders instead of storing anything.
        _batchRepository.RemoveFile(file);
        batch.DownloadedAt = _clock.UtcNow;
        _batchRepository.Update(batch);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A second near-simultaneous download (double-click, a retried request) raced this one:
            // both loaded the row before either committed, this one lost, and its delete now
            // affects zero rows. Same outcome as the row simply being gone — the winner already has
            // the PDF, and the tickets stay valid either way.
            return Result<TicketPrintDownload>.Failure(Error.NotFound(
                "print.file_gone",
                "PDF ovog izvoza više nije dostupan. Ulaznice su i dalje važeće — napravite novi izvoz ako vam treba još primjeraka."));
        }

        _logger.LogInformation(
            "Preuzet PDF izvoza {BatchId} ({Count} ulaznica); pohranjena kopija je obrisana.",
            batchId, batch.TicketCount);

        return Result<TicketPrintDownload>.Success(new TicketPrintDownload(content, FileNameFor(batch)));
    }

    // -------------------------------------------------------------------------------------------
    // Retry
    // -------------------------------------------------------------------------------------------

    public async Task<Result<TicketPrintBatchResponse>> RetryAsync(Guid batchId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, ct);
        if (batch is null)
        {
            return Result<TicketPrintBatchResponse>.Failure(
                Error.NotFound("print.batch_not_found", "Izvoz ulaznica nije pronađen."));
        }

        if (!CanAccess(batch, user))
        {
            return Result<TicketPrintBatchResponse>.Failure(
                Error.Unauthorized("print.forbidden", "Nemate pristup ovom izvozu ulaznica."));
        }

        if (batch.Status != TicketPrintBatchStatus.Failed)
        {
            return Result<TicketPrintBatchResponse>.Failure(Error.Conflict(
                "print.not_retryable", "Ponovni pokušaj je moguć samo za izvoz koji nije uspio."));
        }

        batch.Status = TicketPrintBatchStatus.Queued;
        batch.ErrorMessage = null;
        batch.RenderedCount = 0;
        _batchRepository.Update(batch);
        await _unitOfWork.SaveChangesAsync(ct);

        _queue.Enqueue(batch.Id);

        return Result<TicketPrintBatchResponse>.Success(ToResponse(batch));
    }

    public async Task<Result> DismissAsync(Guid batchId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId, ct);
        if (batch is null)
            return Result.Failure(Error.NotFound("print.batch_not_found", "Izvoz ulaznica nije pronađen."));

        if (!CanAccess(batch, user))
            return Result.Failure(Error.Unauthorized("print.forbidden", "Nemate pristup ovom izvozu ulaznica."));

        // Allowed from any status, including Queued and Rendering: an organizer who no longer
        // wants to watch a render should be able to stop being told about it, and the worker
        // carries on regardless — this only hides the row from the badge.
        //
        // Idempotent. Dismissing twice (a double-click, a stale panel) is not an error worth
        // reporting, and re-stamping would move a timestamp that already means what it says.
        if (batch.DismissedAt is not null)
            return Result.Success();

        batch.DismissedAt = _clock.UtcNow;
        _batchRepository.Update(batch);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // -------------------------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------------------------

    /// <summary>Loads the product from Catalog and checks the caller may act on it. PlatformStaff
    /// bypass the organization check, same as everywhere else in this codebase.</summary>
    private async Task<Result<CatalogProductResponse>> ResolveProductAsync(
        Guid productId, ClaimsPrincipal user, CancellationToken ct)
    {
        var product = await _catalogClient.GetProductAsync(productId, ct);
        if (product is null)
        {
            return Result<CatalogProductResponse>.Failure(
                Error.NotFound("product.not_found", "Proizvod nije pronađen."));
        }

        if (!user.IsPlatformStaff() && user.GetOrganizationId() != product.OrganizationId)
        {
            return Result<CatalogProductResponse>.Failure(
                Error.Unauthorized("product.forbidden", "Proizvod ne pripada vašoj organizaciji."));
        }

        return Result<CatalogProductResponse>.Success(product);
    }

    /// <summary>Null when this mode can be printed. A RecurringReservation sector is one specific
    /// labelled space with Capacity 1 and a Subscription behind it — "print 200 of them" has no
    /// meaning, so it is refused rather than quietly producing 200 sheets for space A-12.</summary>
    private static string? BlockedReasonFor(TicketingMode mode) => mode switch
    {
        TicketingMode.RecurringReservation =>
            "Izvoz fizičkih ulaznica nije moguć za mjesečne rezervacije — svako mjesto se rezerviše pojedinačno.",
        _ => null,
    };

    private static Result ValidateValidDate(TicketingMode mode, DateOnly? validDate)
    {
        if (mode == TicketingMode.DailyEntry)
        {
            return validDate is null
                ? Result.Failure(Error.Validation("print.date_required", "Odaberite datum za koji ulaznice važe."))
                : Result.Success();
        }

        return validDate is null
            ? Result.Success()
            : Result.Failure(Error.Validation("print.date_not_applicable", "Datum se bira samo za dnevne ulaznice."));
    }

    /// <summary>Turns request lines into (sector, ticket type, unit price) triples, rejecting
    /// anything that does not belong to this product. Prices come from the database, never from the
    /// request — an organizer must not be able to print a 5 KM face value onto a 50 KM seat.</summary>
    private static Result<List<ResolvedLine>> ResolveLines(
        CreateTicketPrintBatchRequest request, TicketingMode mode, List<Sector> sectors)
    {
        var resolved = new List<ResolvedLine>(request.Lines.Count);

        foreach (var line in request.Lines)
        {
            var sector = sectors.FirstOrDefault(s => s.Id == line.SectorId);
            if (sector is null)
            {
                return Result<List<ResolvedLine>>.Failure(
                    Error.NotFound("sector.not_found", "Sektor nije pronađen."));
            }

            if (sector.ProductId != request.ProductId)
            {
                return Result<List<ResolvedLine>>.Failure(Error.Validation(
                    "sector.wrong_product", $"Sektor \"{sector.Name}\" ne pripada odabranom proizvodu."));
            }

            if (sector.Status != PublishStatus.Published)
            {
                return Result<List<ResolvedLine>>.Failure(Error.Validation(
                    "sector.not_published", $"Sektor \"{sector.Name}\" nije objavljen."));
            }

            // A DailyEntry sector prices and seats exactly one calendar month; a day outside it has
            // no capacity row at all, and printing against it would invent seats nobody sized.
            if (mode == TicketingMode.DailyEntry && request.ValidDate is { } day
                && (day.Year != sector.PeriodYear || day.Month != sector.PeriodMonth))
            {
                return Result<List<ResolvedLine>>.Failure(Error.Validation(
                    "sector.date_outside_period",
                    $"Sektor \"{sector.Name}\" pokriva {sector.PeriodMonth:00}.{sector.PeriodYear}., a odabrani datum je {day:dd.MM.yyyy}."));
            }

            // Mirrors PurchaseService: a sector either prices through named tiers or through its own
            // Price, never both, and a request must match whichever it is.
            if (sector.TicketTypes.Count > 0)
            {
                if (line.TicketTypeId is null)
                {
                    return Result<List<ResolvedLine>>.Failure(Error.Validation(
                        "tickettype.required", $"Sektor \"{sector.Name}\" zahtijeva odabir vrste ulaznice."));
                }

                var ticketType = sector.TicketTypes.FirstOrDefault(t => t.Id == line.TicketTypeId);
                if (ticketType is null)
                {
                    return Result<List<ResolvedLine>>.Failure(Error.NotFound(
                        "tickettype.not_found", "Vrsta ulaznice nije pronađena."));
                }

                resolved.Add(new ResolvedLine(sector, ticketType.Id, ticketType.Price, line.Quantity));
            }
            else
            {
                if (line.TicketTypeId is not null)
                {
                    return Result<List<ResolvedLine>>.Failure(Error.Validation(
                        "tickettype.not_applicable", $"Sektor \"{sector.Name}\" nema vrste ulaznica."));
                }

                resolved.Add(new ResolvedLine(sector, null, sector.Price, line.Quantity));
            }
        }

        return Result<List<ResolvedLine>>.Success(resolved);
    }

    private bool CanAccess(TicketPrintBatch batch, ClaimsPrincipal user)
        => user.IsPlatformStaff() || user.GetOrganizationId() == batch.OrganizationId;

    private static string FileNameFor(TicketPrintBatch batch)
    {
        var slug = new string([.. batch.ProductName
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')])
            .Trim('-');

        // Collapse the runs of dashes the character-wise replacement above leaves behind, and keep
        // the name short enough to stay comfortable in a file picker.
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        if (slug.Length > 40) slug = slug[..40].Trim('-');
        if (slug.Length == 0) slug = "ulaznice";

        return $"ulaznice-{slug}-{batch.SerialFrom:D6}-{batch.SerialTo:D6}.pdf";
    }

    private static TicketPrintBatchResponse ToResponse(TicketPrintBatch batch) => new(
        batch.Id,
        batch.ProductId,
        batch.ProductName,
        batch.Status,
        batch.TicketCount,
        batch.RenderedCount,
        batch.PageCount,
        batch.SerialFrom,
        batch.SerialTo,
        batch.NominalValue,
        batch.ValidDate,
        batch.FileSizeBytes,
        batch.ErrorMessage,
        batch.CreatedAt,
        batch.CompletedAt,
        batch.DownloadedAt);

    private sealed record ResolvedLine(Sector Sector, Guid? TicketTypeId, decimal UnitPrice, int Quantity);
}
