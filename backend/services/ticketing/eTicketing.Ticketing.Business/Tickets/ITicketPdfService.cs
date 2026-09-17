using System.Security.Claims;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Shared.TicketPdf;
using eTicketing.Ticketing.Business.External;
using eTicketing.Contracts.Security;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace eTicketing.Ticketing.Business.Tickets;

public interface ITicketPdfService
{
    Task<Result<TicketPdfDownload>> GetAsync(Guid ticketId, ClaimsPrincipal user, CancellationToken ct = default);
}
