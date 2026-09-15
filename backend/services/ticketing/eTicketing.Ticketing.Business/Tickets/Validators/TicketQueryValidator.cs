using eTicketing.Contracts.Pagination;
using FluentValidation;

namespace eTicketing.Ticketing.Business.Tickets.Validators;

/// <summary>Paging bounds only; inherits the shared Page/PageSize rules so /tickets/mine behaves
/// like every other paged endpoint. Its absence was invisible rather than loud: TicketEndpoints
/// already declared .WithValidation&lt;TicketQuery&gt;(), but ValidationFilter resolves the
/// validator with GetService and silently skipped when none was registered, leaving the endpoint
/// with no PageSize ceiling at all.</summary>
public class TicketQueryValidator : BaseSearchObjectValidator<TicketQuery>
{
}
