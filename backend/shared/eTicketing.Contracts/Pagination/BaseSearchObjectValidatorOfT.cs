using FluentValidation;

namespace eTicketing.Contracts.Pagination;

/// <summary>
/// Shared paging rules every feature's query-object validator should reuse via
/// <c>Include(new BaseSearchObjectValidator&lt;TQuery&gt;())</c>.
///
/// Generic on purpose: FluentValidation's <c>Include</c> only accepts a validator whose root
/// type matches (or is assignable from, via a shared base) the including validator's root
/// type — a validator built for <see cref="BaseSearchObject"/> itself cannot be included by a
/// validator for a derived query type such as <c>AdminQuery</c>. Instantiating this generic
/// per query type (<c>BaseSearchObjectValidator&lt;AdminQuery&gt;</c>) keeps the rules in one
/// place while satisfying that same-type requirement.
/// </summary>
public class BaseSearchObjectValidator<T> : AbstractValidator<T> where T : BaseSearchObject
{
    public BaseSearchObjectValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Page.HasValue);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .When(x => x.PageSize.HasValue);
    }
}
