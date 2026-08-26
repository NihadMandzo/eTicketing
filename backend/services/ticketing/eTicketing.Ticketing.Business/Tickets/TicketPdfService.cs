using System.Security.Claims;
using eTicketing.Contracts.Results;
using eTicketing.Shared.TicketPdf;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace eTicketing.Ticketing.Business.Tickets;

/// <summary>A rendered ticket, ready to be streamed to the browser.</summary>
public sealed record TicketPdfDownload(byte[] Content, string FileName);

public interface ITicketPdfService
{
    Task<Result<TicketPdfDownload>> GetAsync(Guid ticketId, ClaimsPrincipal user, CancellationToken ct = default);
}

/// <summary>
/// Renders a buyer's ticket on demand, rather than serving a stored file.
///
/// Ticket PDFs are deliberately never persisted anywhere. The sheet is a pure function of the
/// ticket: its QR payload is <see cref="TicketQrCodec.Sign"/> over the ticket id, which is
/// deterministic for a given signing key, so re-rendering produces a byte-identical QR to the one
/// eTicketing.PdfGeneration put in the confirmation e-mail. Keeping a copy in blob storage would
/// mean leaving a gate-opening code sitting at a URL for the lifetime of the account, and buy
/// nothing that this method doesn't.
///
/// The document itself lives in eTicketing.Shared.TicketPdf so this and PdfGeneration render the
/// same sheet from one definition.
/// </summary>
public sealed class TicketPdfService : ITicketPdfService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ICatalogClient _catalogClient;
    private readonly TicketQrCodec _qrCodec;
    private readonly TicketSupportInfo _support;
    private readonly ILogger<TicketPdfService> _logger;

    public TicketPdfService(
        ITicketRepository ticketRepository,
        ICatalogClient catalogClient,
        TicketQrCodec qrCodec,
        IOptions<TicketSupportOptions> support,
        ILogger<TicketPdfService> logger)
    {
        _ticketRepository = ticketRepository;
        _catalogClient = catalogClient;
        _qrCodec = qrCodec;
        _support = new TicketSupportInfo(support.Value.Email, support.Value.Phone);
        _logger = logger;
    }

    public async Task<Result<TicketPdfDownload>> GetAsync(Guid ticketId, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var ticket = await _ticketRepository.GetForPdfAsync(ticketId, ct);
        if (ticket is null)
        {
            return Result<TicketPdfDownload>.Failure(
                Error.NotFound("ticket.not_found", "Ulaznica nije pronađena."));
        }

        // Buyers get their own tickets; platform staff get any, consistent with their override
        // everywhere else. Organizers deliberately do NOT — this sheet carries a working gate code,
        // and an organizer already has the scanner for the only thing they legitimately need.
        if (ticket.UserId != user.GetUserId() && !user.IsPlatformStaff())
        {
            return Result<TicketPdfDownload>.Failure(
                Error.Unauthorized("ticket.forbidden", "Nemate pristup ovoj ulaznici."));
        }

        var product = await _catalogClient.GetProductAsync(ticket.ProductId, ct);
        if (product is null)
        {
            // The product was hard-deleted out from under a sold ticket. Nothing to render a sheet
            // around, and retrying won't help.
            _logger.LogWarning(
                "Proizvod {ProductId} za ulaznicu {TicketId} ne postoji — PDF se ne može generisati.",
                ticket.ProductId, ticketId);
            return Result<TicketPdfDownload>.Failure(
                Error.NotFound("ticket.product_not_found", "Događaj za ovu ulaznicu više ne postoji."));
        }

        var model = new TicketPdfModel(
            ticket.Id,
            ticket.OrderId,
            _qrCodec.Sign(ticket.Id),
            product.Name,
            product.Date,
            product.City.ToString(),
            ticket.Sector?.Name ?? string.Empty,
            ticket.TicketType?.Name,
            ticket.PricePaid,
            product.TicketingMode,
            ticket.ValidDate,
            ticket.ValidFrom,
            ticket.ValidTo,
            ticket.CreatedAt,
            // The buyer's address as stored on the ticket, not the caller's — platform staff
            // downloading someone else's sheet must not see their own e-mail printed on it. A
            // printed ticket has no buyer at all, and says so rather than printing a blank line.
            ticket.UserEmail ?? "Štampana ulaznica");

        var content = new TicketDocument(model, _support).GeneratePdf();

        return Result<TicketPdfDownload>.Success(new TicketPdfDownload(content, model.FileName));
    }
}
