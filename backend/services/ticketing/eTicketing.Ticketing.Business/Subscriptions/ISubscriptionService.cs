using System.Security.Claims;
using eTicketing.Contracts.Pagination;
using eTicketing.Contracts.Results;

namespace eTicketing.Ticketing.Business.Subscriptions;

public interface ISubscriptionService
{
    /// <summary>The caller's own subscriptions. Scoped by the token's user id, never by a
    /// client-supplied one.</summary>
    Task<Result<PagedResult<SubscriptionResponse>>> GetMineAsync(
        SubscriptionQuery query, ClaimsPrincipal user, CancellationToken ct = default);

    /// <summary>
    /// Cancels at the end of the paid-for period: the buyer keeps the month they already paid for,
    /// and the space is only freed when the provider confirms the subscription has actually ended
    /// (see SubscriptionRenewalService.CancelAsync). Owner-only, with the usual PlatformStaff
    /// override.
    /// </summary>
    Task<Result> CancelAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);
}
