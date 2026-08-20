using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Payment.Business.Payments;

namespace eTicketing.Payment.Api.Endpoints;

public static class PaymentEndpoints
{
    // No .AllowAnonymous()/.RequireAuthorization() modifier at all — mirrors Catalog's
    // internal-only GET /internal/products/{id}. Payment never gets JWT auth wired in: it's
    // reachable only inside the Docker network, service-to-service, and never sees a browser-issued
    // token (see Program.cs's own note on this).
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        app.MapPost("/payments", Charge).WithValidation<ChargeRequest>().WithTags("Payments");
    }

    private static async Task<IResult> Charge(ChargeRequest request, IPaymentService service, CancellationToken ct)
    {
        var result = await service.ChargeAsync(request, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }
}
