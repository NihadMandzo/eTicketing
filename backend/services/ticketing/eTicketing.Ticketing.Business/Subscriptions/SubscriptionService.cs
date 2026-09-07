using System.Security.Claims;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Persistence;
using eTicketing.Contracts.Results;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace eTicketing.Ticketing.Business.Subscriptions;

public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IPaymentClient _paymentClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        ISubscriptionRepository subscriptionRepository,
        IPaymentClient paymentClient,
        IUnitOfWork unitOfWork,
        ILogger<SubscriptionService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _paymentClient = paymentClient;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<PagedResult<SubscriptionResponse>>> GetMineAsync(
        SubscriptionQuery query, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var page = await _subscriptionRepository.GetMineAsync(user.GetUserId(), query.Page, query.PageSize, ct);

        return Result<PagedResult<SubscriptionResponse>>.Success(new PagedResult<SubscriptionResponse>
        {
            Items = page.Items.Select(ToResponse).ToList(),
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize,
        });
    }

    public async Task<Result> CancelAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepository.GetByIdWithSectorAsync(id, ct);
        if (subscription is null)
            return Result.Failure(Error.NotFound("subscription.not_found", "Pretplata nije pronađena."));

        // Buyers have no organization, so the usual ownership check is against the user directly.
        // PlatformStaff bypasses it, matching every other ownership check in this service.
        if (subscription.UserId != user.GetUserId() && !user.IsPlatformStaff())
            return Result.Failure(Error.Unauthorized("subscription.forbidden", "Nemate pristup ovoj pretplati."));

        if (subscription.Status == SubscriptionStatus.Cancelled)
            return Result.Failure(Error.Conflict("subscription.already_cancelled", "Pretplata je već otkazana."));

        if (subscription.CancelAtPeriodEnd)
            return Result.Failure(Error.Conflict("subscription.already_cancelled", "Pretplata je već zakazana za otkazivanje."));

        if (string.IsNullOrWhiteSpace(subscription.PaymentReference))
            return Result.Failure(Error.Validation("subscription.not_payable", "Pretplata nema povezano plaćanje."));

        try
        {
            await _paymentClient.CancelSubscriptionAsync(
                subscription.PaymentReference, atPeriodEnd: true, refundLastInvoice: false, ct);
        }
        catch (PaymentUnavailableException)
        {
            return Result.Failure(Error.Failure("payment.unavailable", "Plaćanje trenutno nije dostupno. Pokušajte kasnije."));
        }

        // Status stays Active and the space stays taken: the buyer paid for this period and their
        // ticket is valid until it ends. Only the provider's cancellation webhook flips the status
        // and hands the space back (see SubscriptionRenewalService.CancelAsync).
        subscription.CancelAtPeriodEnd = true;
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Pretplata {SubscriptionId} je zakazana za otkazivanje na kraju perioda {PeriodEnd}.",
            subscription.Id, subscription.CurrentPeriodEnd);

        return Result.Success();
    }

    private static SubscriptionResponse ToResponse(Subscription subscription) =>
        new(subscription.Id,
            subscription.SectorId,
            subscription.Sector?.Name ?? string.Empty,
            subscription.Sector?.ProductId ?? Guid.Empty,
            subscription.Status,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.NextRenewalAt,
            subscription.CancelAtPeriodEnd,
            subscription.CancelledAt,
            subscription.Sector?.Price ?? 0m);
}
