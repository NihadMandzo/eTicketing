using eTicketing.Contracts.Results;
using eTicketing.Contracts.Validation;
using eTicketing.Payment.Business.Payments;
using eTicketing.Payment.Business.Payments.Webhooks;

namespace eTicketing.Payment.Api.Endpoints;

public static class PaymentEndpoints
{
    /// <summary>
    /// No .RequireAuthorization()/.AllowAnonymous() anywhere, and Program.cs wires no authentication
    /// at all -- deliberate, and unchanged from before Stripe. Every /payments/* route below is
    /// reachable only from inside the Docker network (the service publishes no host port and the
    /// Gateway routes nothing to it), so isolation is network segmentation, not JWT.
    ///
    /// The webhook is the single exception and the only publicly routed path on this service. It
    /// cannot use a cookie or a bearer token -- Stripe has neither -- so it authenticates itself
    /// with a signature over the raw request body instead. The Gateway matches its EXACT path, never
    /// a catch-all under /api/payments, which is what keeps the endpoints above unreachable from the
    /// internet.
    /// </summary>
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/payments").WithTags("Payments");

        group.MapPost("/intents", CreateIntent).WithValidation<CreateIntentRequest>();
        group.MapPost("/capture", Capture).WithValidation<CapturePaymentRequest>();
        group.MapPost("/subscriptions/confirm", ConfirmSubscription).WithValidation<ConfirmSubscriptionRequest>();
        group.MapPost("/intents/cancel", CancelIntent).WithValidation<CancelIntentRequest>();
        group.MapPost("/subscriptions/cancel", CancelSubscription).WithValidation<CancelSubscriptionRequest>();

        app.MapPost("/payments/webhook", Webhook).WithTags("Payments");
    }

    private static async Task<IResult> CreateIntent(CreateIntentRequest request, IPaymentService service, CancellationToken ct)
    {
        var result = await service.CreateIntentAsync(request, ct);
        return result.ToHttpResult(StatusCodes.Status201Created);
    }

    private static async Task<IResult> Capture(CapturePaymentRequest request, IPaymentService service, CancellationToken ct)
    {
        var result = await service.CaptureAsync(request, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> ConfirmSubscription(ConfirmSubscriptionRequest request, IPaymentService service, CancellationToken ct)
    {
        var result = await service.ConfirmSubscriptionAsync(request, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> CancelIntent(CancelIntentRequest request, IPaymentService service, CancellationToken ct)
    {
        var result = await service.CancelIntentAsync(request, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> CancelSubscription(CancelSubscriptionRequest request, IPaymentService service, CancellationToken ct)
    {
        var result = await service.CancelSubscriptionAsync(request, ct);
        return result.ToHttpResult();
    }

    /// <summary>
    /// Takes HttpRequest and reads the body itself rather than binding a DTO, and carries no
    /// .WithValidation&lt;T&gt;() -- both on purpose. Stripe signs the exact bytes it sent, so anything
    /// that deserializes and re-serializes the payload (model binding, a validation filter reading
    /// the body) destroys the signature and every legitimate delivery starts failing.
    /// </summary>
    private static async Task<IResult> Webhook(HttpRequest request, IStripeWebhookService service, CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        var signature = request.Headers["Stripe-Signature"].FirstOrDefault();

        var result = await service.HandleAsync(rawBody, signature, ct);
        return result.ToHttpResult();
    }
}
