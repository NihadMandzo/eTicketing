using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.Tickets;

/// <summary>
/// The gate. Two operations, both organizer-facing:
///
///  - <see cref="GetProductsForTodayAsync"/> — "what am I checking people into today", scoped to
///    the caller's organization.
///  - <see cref="ValidateAsync"/> — scan one code against one product, and if it's good, burn it.
///
/// Two things about the shape of this class are deliberate and worth not undoing:
///
/// 1. <b>Validation is always against a product.</b> There is no "is this ticket valid?" method,
///    only "is this ticket valid FOR THIS PRODUCT?". A ticket to last night's concert is a
///    perfectly good ticket and still must not get anyone into today's football match.
/// 2. <b>A bad ticket is a successful Result.</b> Already used, wrong event, expired, forged — all
///    come back as Result.Success with IsValid=false and a Bosnian Message the scanner renders
///    verbatim on a red card. Only "caller is not an organizer" (403) and "another scanner holds
///    the lock" (409) are Result.Failure, because neither is an answer about a ticket.
/// </summary>
public class TicketValidationService : ITicketValidationService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketValidationLock _validationLock;
    private readonly ICatalogClient _catalogClient;
    private readonly TicketQrCodec _qrCodec;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TicketValidationService> _logger;

    public TicketValidationService(
        ITicketRepository ticketRepository,
        ITicketValidationLock validationLock,
        ICatalogClient catalogClient,
        TicketQrCodec qrCodec,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<TicketValidationService> logger)
    {
        _ticketRepository = ticketRepository;
        _validationLock = validationLock;
        _catalogClient = catalogClient;
        _qrCodec = qrCodec;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
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

    public async Task<Result<TicketValidationResponse>> ValidateAsync(ValidateTicketRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (!_qrCodec.TryParse(request.Code, out var ticketId))
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
            return await ValidateLockedAsync(request, ticketId, user, ct);
        }
        finally
        {
            // finally, not a happy-path call: an exception here would otherwise leave the ticket
            // unscannable until the 10s TTL lapsed, with a queue waiting at the door.
            await _validationLock.ReleaseAsync(ticketId, lockToken, CancellationToken.None);
        }
    }

    private async Task<Result<TicketValidationResponse>> ValidateLockedAsync(
        ValidateTicketRequest request, Guid ticketId, ClaimsPrincipal user, CancellationToken ct)
    {
        var ticket = await _ticketRepository.GetForValidationAsync(ticketId, ct);
        if (ticket is null)
            return Invalid("ticket.not_found", "Ulaznica ne postoji.");

        if (!user.IsPlatformStaff() && ticket.Sector?.OrganizationId != user.GetOrganizationId())
            return Invalid("ticket.wrong_organization", "Ulaznica ne pripada vašoj organizaciji.", ticket);

        // The requirement this whole feature exists for: not "is the ticket valid" but "is it valid
        // for the event whose gate this scanner is standing at".
        if (ticket.ProductId != request.ProductId)
            return Invalid("ticket.wrong_product", "Ulaznica ne pripada odabranom događaju.", ticket);

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

        ticket.MarkValidated(user.GetUserId(), _timeProvider.GetUtcNow().UtcDateTime);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Ulaznica {TicketId} za proizvod {ProductId} validirana od strane {UserId}.",
            ticket.Id, ticket.ProductId, user.GetUserId());

        return Result<TicketValidationResponse>.Success(new TicketValidationResponse(
            IsValid: true,
            Code: "ticket.valid",
            Message: "Ulaznica je validna. Ulaz odobren.",
            TicketId: ticket.Id,
            SectorName: ticket.Sector?.Name,
            TicketTypeName: ticket.TicketType?.Name,
            HolderEmail: ticket.UserEmail,
            ValidatedAt: ticket.ValidatedAt));
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

    private DateOnly Today() => DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

    private static Result<TicketValidationResponse> Invalid(string code, string message, Ticket? ticket = null) =>
        Result<TicketValidationResponse>.Success(new TicketValidationResponse(
            IsValid: false,
            Code: code,
            Message: message,
            TicketId: ticket?.Id,
            SectorName: ticket?.Sector?.Name,
            TicketTypeName: ticket?.TicketType?.Name,
            HolderEmail: ticket?.UserEmail,
            ValidatedAt: ticket?.ValidatedAt));
}
