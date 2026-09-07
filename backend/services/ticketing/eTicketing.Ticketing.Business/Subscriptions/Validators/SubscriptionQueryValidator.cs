using eTicketing.Contracts.Pagination;
using FluentValidation;

namespace eTicketing.Ticketing.Business.Subscriptions.Validators;

/// <summary>Paging bounds only; inherits the shared Page/PageSize rules so /subscriptions/mine
/// behaves like every other paged endpoint.</summary>
public class SubscriptionQueryValidator : BaseSearchObjectValidator<SubscriptionQuery>
{
}
