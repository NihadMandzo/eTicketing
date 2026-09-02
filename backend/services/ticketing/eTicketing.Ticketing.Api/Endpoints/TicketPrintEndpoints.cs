using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Ticketing.Business.TicketPrint;

namespace eTicketing.Ticketing.Api.Endpoints;

public static class TicketPrintEndpoints
{
    public static void MapTicketPrintEndpoints(this WebApplication app)
    {
        // "Organizer" also admits Admin/SuperAdmin (see AuthorizationPolicyExtensions), which is
        // the PlatformStaff override — TicketPrintService then skips the per-organization ownership
        // check for them. Buyers have no business here at all: these sheets are unsold inventory.
        var group = app.MapGroup("/ticket-print-batches")
            .WithTags("TicketPrint")
            .RequireAuthorization("Organizer");

        group.MapGet("/options", GetOptions);
        group.MapGet("/outstanding", GetOutstanding);
        group.MapGet("/latest", GetLatest);
        group.MapPost("/", Create).WithValidation<CreateTicketPrintBatchRequest>();
        group.MapGet("/{id:guid}", Get);
        group.MapGet("/{id:guid}/file", Download);
        group.MapPost("/{id:guid}/retry", Retry);
        // POST, not DELETE: this clears the batch from the organizer's badge, it does not delete
        // the batch (whose serial range and nominal value stay on record) and it never touches
        // the tickets. A DELETE would advertise the wrong thing.
        group.MapPost("/{id:guid}/dismiss", Dismiss);
    }

    private static async Task<IResult> GetOptions(
        Guid productId, DateOnly? date, ITicketPrintService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetOptionsAsync(productId, date, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetOutstanding(ITicketPrintService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetOutstandingAsync(http.User, ct);
        return result.ToHttpResult();
    }

    /// <summary>204 rather than 200-with-nothing when the product has never been exported.
    /// ToHttpResult would serialize a null value as an empty body, which a JSON client cannot tell
    /// apart from a malformed response — an explicit "no content" says the lookup succeeded and
    /// found nothing.</summary>
    private static async Task<IResult> GetLatest(
        Guid productId, ITicketPrintService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetLatestForProductAsync(productId, http.User, ct);

        if (result.IsSuccess && result.Value is null)
        {
            return Results.NoContent();
        }

        return result.ToHttpResult();
    }

    private static async Task<IResult> Create(
        CreateTicketPrintBatchRequest request, ITicketPrintService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.CreateAsync(request, http.User, ct);
        return result.IsSuccess
            ? Results.Created($"/ticket-print-batches/{result.Value!.Id}", result.Value)
            : result.ToHttpResult();
    }

    private static async Task<IResult> Get(Guid id, ITicketPrintService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.GetAsync(id, http.User, ct);
        return result.ToHttpResult();
    }

    /// <summary>Hands over the rendered sheet and destroys the stored copy in the same transaction,
    /// so a batch downloads exactly once. An attachment rather than inline: this is hundreds of
    /// pages headed for a printer, not something to read in a browser tab.</summary>
    private static async Task<IResult> Download(Guid id, ITicketPrintService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.DownloadAsync(id, http.User, ct);
        if (!result.IsSuccess)
        {
            return result.ToHttpResult();
        }

        var pdf = result.Value!;
        http.Response.Headers.ContentDisposition = $"attachment; filename=\"{pdf.FileName}\"";
        return Results.File(pdf.Content, "application/pdf");
    }

    private static async Task<IResult> Retry(Guid id, ITicketPrintService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.RetryAsync(id, http.User, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> Dismiss(Guid id, ITicketPrintService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.DismissAsync(id, http.User, ct);
        return result.ToHttpResult();
    }
}
