using eTicketing.Contracts.Pagination;
using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Business.Subscriptions;

/// <summary>Paging only -- a buyer's subscription list has nothing to filter or search by.</summary>
public record SubscriptionQuery : BaseSearchObject;
