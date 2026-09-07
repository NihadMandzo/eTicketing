using eTicketing.Contracts.Pagination;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Business.Subscriptions;

/// <param name="CancelAtPeriodEnd">The buyer has cancelled but the period they paid for is still
/// running, so Status is deliberately still Active. The UI has to say this out loud, or a cancelled
/// subscription looks like the cancellation did not take.</param>
public record SubscriptionResponse(
    Guid Id,
    Guid SectorId,
    string SectorName,
    Guid ProductId,
    SubscriptionStatus Status,
    DateOnly CurrentPeriodStart,
    DateOnly CurrentPeriodEnd,
    DateTime? NextRenewalAt,
    bool CancelAtPeriodEnd,
    DateTime? CancelledAt,
    decimal PricePerPeriod);

/// <summary>Paging only -- a buyer's subscription list has nothing to filter or search by.</summary>
public record SubscriptionQuery : BaseSearchObject;
