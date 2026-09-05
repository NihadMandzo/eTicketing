using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Payment.Business.Payments.Gateways;
using eTicketing.Payment.Data.Entities;
using eTicketing.Payment.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PaymentEntity = eTicketing.Payment.Data.Entities.Payment;

namespace eTicketing.Payment.Business.Payments;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IStripeCustomerRepository _customerRepository;
    private readonly IPaymentGateway _gateway;
    private readonly StripeOptions _stripeOptions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentRepository paymentRepository,
        IStripeCustomerRepository customerRepository,
        IPaymentGateway gateway,
        IOptions<StripeOptions> stripeOptions,
        IUnitOfWork unitOfWork,
        ILogger<PaymentService> logger)
    {
        _paymentRepository = paymentRepository;
        _customerRepository = customerRepository;
        _gateway = gateway;
        _stripeOptions = stripeOptions.Value;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<PaymentIntentResponse>> CreateIntentAsync(CreateIntentRequest request, CancellationToken ct = default)
    {
        var currency = _stripeOptions.Currency;
        var metadata = BuildMetadata(request);

        try
        {
            if (request.IsSubscription)
                return await CreateSubscriptionIntentAsync(request, currency, metadata, ct);

            // The gateway is handed OrderRef as its idempotency key, so a replayed create returns
            // the SAME provider intent -- including its client secret -- rather than a second one.
            // That is why this does not need to short-circuit on an existing row: the provider is
            // already the authority on uniqueness, and the local row is simply brought up to date.
            var intent = await _gateway.CreateIntentAsync(
                new GatewayIntentRequest(
                    request.Amount, currency, request.OrderRef, request.UserId,
                    request.CustomerEmail, request.Description, metadata),
                ct);

            await UpsertPendingAsync(request, currency, intent.IntentId, providerSubscriptionId: null, ct);

            return Result<PaymentIntentResponse>.Success(new PaymentIntentResponse(
                _gateway.Name, _gateway.PublishableKey, intent.IntentId, intent.ClientSecret,
                request.Amount, currency, SubscriptionReference: null));
        }
        catch (PaymentGatewayUnavailableException ex)
        {
            return ProviderUnavailable<PaymentIntentResponse>(ex, request.OrderRef, "kreiranje namjere plaćanja");
        }
    }

    private async Task<Result<PaymentIntentResponse>> CreateSubscriptionIntentAsync(
        CreateIntentRequest request,
        string currency,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SubscriptionProductName))
            return Result<PaymentIntentResponse>.Failure(Error.Validation(
                "payment.subscription_name_required", "Naziv pretplate je obavezan."));

        if (string.IsNullOrWhiteSpace(request.CustomerEmail))
            return Result<PaymentIntentResponse>.Failure(Error.Validation(
                "payment.customer_email_required", "Email kupca je obavezan za pretplatu."));

        var customerReference = await ResolveCustomerAsync(request.UserId, request.CustomerEmail, ct);

        var subscription = await _gateway.CreateSubscriptionAsync(
            new GatewaySubscriptionRequest(
                request.Amount, currency, request.OrderRef, request.UserId,
                customerReference, request.SubscriptionProductName, metadata),
            ct);

        await UpsertPendingAsync(request, currency, subscription.ClientSecret is null ? null : subscription.SubscriptionId,
            subscription.SubscriptionId, ct);

        return Result<PaymentIntentResponse>.Success(new PaymentIntentResponse(
            _gateway.Name, _gateway.PublishableKey, subscription.SubscriptionId, subscription.ClientSecret,
            request.Amount, currency, subscription.SubscriptionId));
    }
