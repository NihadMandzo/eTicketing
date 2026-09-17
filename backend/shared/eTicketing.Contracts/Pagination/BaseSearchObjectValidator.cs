using FluentValidation;

namespace eTicketing.Contracts.Pagination;

/// <summary>
/// Concrete validator for endpoints that bind <see cref="BaseSearchObject"/> directly (no
/// derived query type) — e.g. <c>GetUsers</c>. Registered by assembly scanning like any other
/// FluentValidation validator, unlike the open generic above.
/// </summary>
public sealed class BaseSearchObjectValidator : BaseSearchObjectValidator<BaseSearchObject>;
