using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.External;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.Tickets;

/// <summary>
/// The gate. Three operations:
///
///  - <see cref="GetProductsForTodayAsync"/> — "what am I checking people into today", scoped to
///    the caller's organization.
///  - <see cref="ValidateAsync"/> — an organizer scans one code, and if it's good, burns it.
///  - <see cref="ValidateForDeviceAsync"/> — the same, for an unattended scanner whose scope comes
///    from its own registration rather than from anything it sends.
///
/// Three things about the shape of this class are deliberate and worth not undoing:
///
/// 1. <b>Validation is always against a product, and optionally against specific sectors.</b> There
///    is no "is this ticket valid?" method, only "is this ticket valid FOR THIS DOOR?". A ticket to
///    last night's concert is a perfectly good ticket and still must not get anyone into today's
///    football match — and a Parter ticket must not get anyone into VIP.
/// 2. <b>A bad ticket is a successful Result.</b> Already used, wrong event, wrong sector, expired,
///    forged — all come back as Result.Success with IsValid=false and a Bosnian Message the scanner
///    renders verbatim on a red card. Only "caller is not an organizer" (403) and "another scanner
///    holds the lock" (409) are Result.Failure, because neither is an answer about a ticket.
/// 3. <b>The device never states its own scope.</b> <see cref="ValidateForDeviceAsync"/> takes the
///    GateDevice row, not a product id off the wire, so tampered firmware cannot widen what its
///    door admits.
/// 4. <b>Not every ticket is spent by being used.</b> SingleOccurrence and DailyEntry tickets are
///    one admission each and get burnt to <see cref="TicketStatus.Used"/> on their only scan.
///    A RecurringReservation ticket is a month of parking, not one drive-in - burning it would
///    lock its holder out from the second day onwards. Those scans toggle
///    <see cref="Ticket.IsInside"/> instead: a scan while outside is an entry, a scan while inside
///    is an exit, and the ticket stays Confirmed for its whole ValidFrom..ValidTo window. Because
///    that toggle is the only way in, the same ticket can never be used to enter twice without an
///    exit in between - which is exactly the rule this mode needs.
/// </summary>
public class TicketValidationService : ITicketValidationService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketValidationLock _validationLock;
    private readonly ICatalogClient _catalogClient;
    private readonly TicketQrCodec _qrCodec;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PlatformClock _clock;
    private readonly ILogger<TicketValidationService> _logger;

    public TicketValidationService(
        ITicketRepository ticketRepository,
        ITicketValidationLock validationLock,
        ICatalogClient catalogClient,
        TicketQrCodec qrCodec,
        IUnitOfWork unitOfWork,
        PlatformClock clock,
        ILogger<TicketValidationService> logger)
    {
        _ticketRepository = ticketRepository;
        _validationLock = validationLock;
        _catalogClient = catalogClient;
        _qrCodec = qrCodec;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<List<ValidationProductResponse>>> GetProductsForTodayAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        // PlatformStaff passes null → no organization filter, matching the platform-wide override
        // they already hold over every product/sector/ticket in .claude/rules/01-domain.md.
        var isPlatformStaff = user.IsPlatformStaff();
        var organizationId = isPlatformStaff ? null : user.GetOrganizationId();
        if (organizationId is null && !isPlatformStaff)
            return Result<List<ValidationProductResponse>>.Failure(Error.Unauthorized("ticket.no_organization", "Nalog nije vezan za organizaciju."));

        var today = Today();
        var counts = await _ticketRepository.GetValidationCountsAsync(organizationId, today, ct);
        if (counts.Count == 0)
            return Result<List<ValidationProductResponse>>.Success([]);

        var products = await _catalogClient.GetProductsAsync(counts.Select(c => c.ProductId).Distinct().ToList(), ct);
        var productsById = products.ToDictionary(p => p.Id);

        var result = new List<ValidationProductResponse>();
        foreach (var count in counts)
        {
            // A product Catalog no longer knows about (deleted out from under live tickets) is
            // skipped rather than shown as a nameless row — there is nothing useful to scan against.
            if (!productsById.TryGetValue(count.ProductId, out var product))
                continue;

            // SingleOccurrence tickets carry no date of their own, so the repository could not
            // filter them — this is where their real showing date finally gets checked.
            if (count.TicketingMode == TicketingMode.SingleOccurrence
                && (product.Date is null || DateOnly.FromDateTime(product.Date.Value) != today))
            {
                continue;
            }

            result.Add(new ValidationProductResponse(
                product.Id, product.Name, product.Date, count.TicketingMode, count.TotalToday, count.ValidatedToday));
        }

        return Result<List<ValidationProductResponse>>.Success(
            result.OrderBy(p => p.Date ?? DateTime.MaxValue).ThenBy(p => p.Name).ToList());
    }

    public Task<Result<TicketValidationResponse>> ValidateAsync(ValidateTicketRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var isPlatformStaff = user.IsPlatformStaff();

        var scope = new ValidationScope(
            BypassOrganizationCheck: isPlatformStaff,
            OrganizationId: isPlatformStaff ? null : user.GetOrganizationId(),
            ProductId: request.ProductId,
            // Absent or empty means the caller did not narrow to a sector, which is the behavior
            // every existing client relies on — the mobile scanner opens on a product, not a door.
            SectorIds: request.SectorIds is { Count: > 0 } ids ? ids.ToHashSet() : null,
            ValidatedByUserId: user.GetUserId(),
            ValidatedByDeviceId: null);

        return ValidateCoreAsync(request.Code, scope, ct);
    }

    public Task<Result<TicketValidationResponse>> ValidateForDeviceAsync(GateDevice device, string code, CancellationToken ct = default)
    {
        var scope = new ValidationScope(
            // A device is always bound to one organization; there is no device equivalent of
            // PlatformStaff, and there should not be one.
            BypassOrganizationCheck: false,
            OrganizationId: device.OrganizationId,
            ProductId: device.ProductId,
            SectorIds: device.AllSectors ? null : device.Sectors.Select(s => s.SectorId).ToHashSet(),
            // The organizer who registered the gate stays the accountable party for what it admits.
            ValidatedByUserId: device.CreatedByUserId,
            ValidatedByDeviceId: device.Id);

        return ValidateCoreAsync(code, scope, ct);
    }

    private async Task<Result<TicketValidationResponse>> ValidateCoreAsync(string code, ValidationScope scope, CancellationToken ct)
    {
        if (!_qrCodec.TryParse(code, out var ticketId))
            return Invalid("ticket.qr_invalid", "Kod nije prepoznat. Ovo nije važeća eKarta ulaznica.");

        var lockToken = await _validationLock.TryAcquireAsync(ticketId, ct);
        if (lockToken is null)
        {
            // Someone else is mid-validation on this exact ticket right now. 409 rather than an
            // IsValid=false card: this says nothing about the ticket, only about timing, so the
            // scanner should invite a retry instead of turning the holder away.
            return Result<TicketValidationResponse>.Failure(Error.Conflict(
                "ticket.validation_in_progress", "Ova ulaznica se trenutno provjerava na drugom uređaju. Pokušajte ponovo."));
        }

        try
        {
            return await ValidateLockedAsync(ticketId, scope, ct);
        }
        finally
        {
            // finally, not a happy-path call: an exception here would otherwise leave the ticket
            // unscannable until the 10s TTL lapsed, with a queue waiting at the door.
            await _validationLock.ReleaseAsync(ticketId, lockToken, CancellationToken.None);
        }
    }

    private async Task<Result<TicketValidationResponse>> ValidateLockedAsync(Guid ticketId, ValidationScope scope, CancellationToken ct)
    {
        var ticket = await _ticketRepository.GetForValidationAsync(ticketId, ct);
        if (ticket is null)
            return Invalid("ticket.not_found", "Ulaznica ne postoji.");

        if (!scope.BypassOrganizationCheck && ticket.Sector?.OrganizationId != scope.OrganizationId)
            return Invalid("ticket.wrong_organization", "Ulaznica ne pripada vašoj organizaciji.", ticket);

        // The requirement this whole feature exists for: not "is the ticket valid" but "is it valid
        // for the event whose gate this scanner is standing at".
        if (ticket.ProductId != scope.ProductId)
            return Invalid("ticket.wrong_product", "Ulaznica ne pripada odabranom događaju.", ticket);

        // ...and then, one level finer, the right door of that event. Checked after the product so a
        // ticket for a different event entirely still reports the more useful reason: telling
        // someone at the VIP door that their ticket is "for another sector" when it is actually for
        // last week's match would send them to the wrong place to fix it.
        if (scope.SectorIds is { } allowedSectors && !allowedSectors.Contains(ticket.SectorId))
            return Invalid("ticket.wrong_sector", "Ulaznica ne pripada sektoru na ovom ulazu.", ticket);

        switch (ticket.Status)
        {
            case TicketStatus.Used:
                return Invalid("ticket.already_used", "Ulaznica je već iskorištena.", ticket);
            case TicketStatus.Cancelled:
                return Invalid("ticket.cancelled", "Ulaznica je otkazana.", ticket);
            case TicketStatus.Processing:
                return Invalid("ticket.not_confirmed", "Ulaznica još nije potvrđena.", ticket);
        }

        var validityError = await CheckValidTodayAsync(ticket, ct);
        if (validityError is not null)
            return Invalid(validityError.Value.Code, validityError.Value.Message, ticket);

        return IsMultiPassage(ticket)
            ? await AdmitMultiPassageAsync(ticket, scope, ct)
            : await AdmitSinglePassageAsync(ticket, scope, ct);
    }

    /// <summary>A RecurringReservation ticket, told apart by the period columns only that mode
    /// populates. Deliberately read off the ticket rather than off Sector.TicketingMode: the two
    /// cannot disagree (Ticket's factories enforce it) and this needs no navigation loaded.</summary>
    private static bool IsMultiPassage(Ticket ticket) => ticket.ValidFrom is not null && ticket.ValidTo is not null;

    /// <summary>SingleOccurrence and DailyEntry: one admission, then the ticket is spent.</summary>
    private async Task<Result<TicketValidationResponse>> AdmitSinglePassageAsync(
        Ticket ticket, ValidationScope scope, CancellationToken ct)
    {
        ticket.MarkValidated(scope.ValidatedByUserId, _clock.UtcNow, scope.ValidatedByDeviceId);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Ulaznica {TicketId} za proizvod {ProductId} validirana od strane {UserId} (uređaj: {DeviceId}).",
            ticket.Id, ticket.ProductId, scope.ValidatedByUserId, scope.ValidatedByDeviceId);

        return Valid("ticket.valid", "Ulaznica je validna. Ulaz odobren.", ticket, GatePassageDirection.Entry);
    }

    /// <summary>
    /// RecurringReservation: the ticket is a period, not a single admission, so a scan moves the
    /// holder through the gate in whichever direction they are not currently in.
    ///
    /// Reading the direction off stored state rather than taking it from the scanner is what makes
    /// the "no two entries without an exit" rule hold at all: there is no request field a caller
    /// could set to enter twice, and a second consecutive scan is the exit it actually is.
    /// </summary>
    private async Task<Result<TicketValidationResponse>> AdmitMultiPassageAsync(
        Ticket ticket, ValidationScope scope, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var isExit = ticket.IsInside;

        if (isExit)
            ticket.RegisterExit(scope.ValidatedByUserId, now, scope.ValidatedByDeviceId);
        else
            ticket.RegisterEntry(scope.ValidatedByUserId, now, scope.ValidatedByDeviceId);

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Pretplatnička ulaznica {TicketId} za proizvod {ProductId}: {Direction} zabilježen od strane {UserId} (uređaj: {DeviceId}).",
            ticket.Id, ticket.ProductId, isExit ? "izlaz" : "ulaz", scope.ValidatedByUserId, scope.ValidatedByDeviceId);

        return isExit
            ? Valid("ticket.exit_recorded", "Izlaz zabilježen. Ulaznica je ponovo spremna za ulaz.", ticket, GatePassageDirection.Exit)
            : Valid("ticket.valid", "Ulaznica je validna. Ulaz odobren.", ticket, GatePassageDirection.Entry);
    }

    /// <summary>Mode-aware "is today inside this ticket's window". DailyEntry and
    /// RecurringReservation answer from their own columns; SingleOccurrence has to ask Catalog for
    /// the showing date, which is why this is async. A Catalog outage surfaces as the thrown
    /// exception (and 500) it really is, rather than silently admitting everyone.</summary>
    private async Task<(string Code, string Message)?> CheckValidTodayAsync(Ticket ticket, CancellationToken ct)
    {
        var today = Today();

        if (ticket.ValidDate is { } validDate)
        {
            return validDate == today
                ? null
                : ("ticket.not_valid_today", $"Ulaznica vrijedi za {validDate:dd.MM.yyyy}., a ne za danas.");
        }

        if (ticket.ValidFrom is { } from && ticket.ValidTo is { } to)
        {
            return today >= from && today <= to
                ? null
                : ("ticket.not_valid_today", $"Rezervacija vrijedi od {from:dd.MM.yyyy}. do {to:dd.MM.yyyy}.");
        }

        var product = await _catalogClient.GetProductAsync(ticket.ProductId, ct);
        if (product?.Date is null)
            return ("ticket.product_unavailable", "Podaci o događaju trenutno nisu dostupni.");

        return DateOnly.FromDateTime(product.Date.Value) == today
            ? null
            : ("ticket.not_valid_today", $"Ulaznica vrijedi za {product.Date.Value:dd.MM.yyyy}., a ne za danas.");
    }

    /// <summary>The platform's local business day — deliberately not UTC's. See PlatformClock: a
    /// ticket's ValidDate and a product's Date are local wall-clock dates, so comparing them against
    /// a UTC-derived "today" rejects valid tickets for the first hour or two after local midnight.</summary>
    private DateOnly Today() => _clock.Today();

    private static Result<TicketValidationResponse> Valid(
        string code, string message, Ticket ticket, GatePassageDirection direction) =>
        Result<TicketValidationResponse>.Success(new TicketValidationResponse(
            IsValid: true,
            Code: code,
            Message: message,
            TicketId: ticket.Id,
            SectorName: ticket.Sector?.Name,
            TicketTypeName: ticket.TicketType?.Name,
            HolderEmail: ticket.UserEmail,
            ValidatedAt: ticket.ValidatedAt,
            Direction: direction,
            IsInside: ticket.IsInside));

    private static Result<TicketValidationResponse> Invalid(string code, string message, Ticket? ticket = null) =>
        Result<TicketValidationResponse>.Success(new TicketValidationResponse(
            IsValid: false,
            Code: code,
            Message: message,
            TicketId: ticket?.Id,
            SectorName: ticket?.Sector?.Name,
            TicketTypeName: ticket?.TicketType?.Name,
            HolderEmail: ticket?.UserEmail,
            ValidatedAt: ticket?.ValidatedAt,
            Direction: GatePassageDirection.None,
            IsInside: ticket?.IsInside ?? false));
}
