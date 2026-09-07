using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Ticketing.Business.Purchases;

namespace eTicketing.Ticketing.Api.Endpoints;

public static class PurchaseEndpoints
{
    public static void MapPurchaseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/purchases").WithTags("Purchases");

        // Any authenticated user may purchase — same "no ownership concept for buyers" reasoning
        // as Sector.Hold (see SectorEndpoints.Hold).
        //
        // Two calls, because the card never touches this platform: the first prices the hold and
        // returns the payment object the browser confirms directly with the provider, the second
        // captures it and mints the tickets.
        group.MapPost("/payment-intent", CreatePaymentIntent).RequireAuthorization().WithValidation<CreatePaymentIntentRequest>();
        group.MapPost("", Purchase).RequireAuthorization().WithValidation<PurchaseRequest>();
    }

    private static async Task<IResult> CreatePaymentIntent(
        CreatePaymentIntentRequest request, IPurchaseService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.CreatePaymentIntentAsync(request, http.User, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> Purchase(PurchaseRequest request, IPurchaseService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.PurchaseAsync(request, http.User, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }
}
