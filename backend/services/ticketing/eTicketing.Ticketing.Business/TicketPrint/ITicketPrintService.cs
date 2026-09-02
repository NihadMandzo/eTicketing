using System.Security.Claims;
using eTicketing.Contracts.Results;

namespace eTicketing.Ticketing.Business.TicketPrint;

/// <summary>
/// Bulk issuance of physical, printable tickets for a product — the box-office counterpart to
/// PurchaseService. Everything an organizer does with a print batch goes through here.
/// </summary>
public interface ITicketPrintService
{
    /// <summary>What the export screen can offer: sectors, price tiers, remaining capacity, and
    /// whether this product's TicketingMode supports printing at all. <paramref name="date"/>
    /// applies to DailyEntry products only, where capacity is tracked per calendar day — the screen
    /// re-fetches with the chosen date so "preostalo" means what it says.</summary>
    Task<Result<TicketPrintOptionsResponse>> GetOptionsAsync(
        Guid productId, DateOnly? date, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Mints the tickets, claims their capacity and queues the render. Returns as soon as
    /// the rows are committed — the PDF is built afterwards, off the request thread.</summary>
    Task<Result<TicketPrintBatchResponse>> CreateAsync(CreateTicketPrintBatchRequest request, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Status poll for one batch.</summary>
    Task<Result<TicketPrintBatchResponse>> GetAsync(Guid batchId, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Every batch of the caller's organization still worth showing them — backs the
    /// desktop's "your PDF is ready" badge.</summary>
    Task<Result<List<TicketPrintBatchResponse>>> GetOutstandingAsync(ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>The most recent batch for a product, so re-opening the export screen resumes a
    /// render the organizer walked away from. Null value when the product has never been exported.</summary>
    Task<Result<TicketPrintBatchResponse?>> GetLatestForProductAsync(Guid productId, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Hands the rendered sheet over and destroys the stored copy in the same transaction —
    /// a batch can be downloaded exactly once.</summary>
    Task<Result<TicketPrintDownload>> DownloadAsync(Guid batchId, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Re-queues a failed render. The tickets already exist and their capacity is already
    /// claimed, so this never touches either — it only asks for the paper again.</summary>
    Task<Result<TicketPrintBatchResponse>> RetryAsync(Guid batchId, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>Clears a batch from the organizer's notification badge. Stamps
    /// <c>TicketPrintBatch.DismissedAt</c> — it does not delete the batch, whose serial range and
    /// nominal value stay on record, and it never touches the tickets, which remain valid and
    /// sellable. The one way to clear a Failed or Expired row, which nothing else can remove.</summary>
    Task<Result> DismissAsync(Guid batchId, ClaimsPrincipal user, CancellationToken ct = default);
}
