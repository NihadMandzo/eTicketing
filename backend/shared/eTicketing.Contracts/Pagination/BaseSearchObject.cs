namespace eTicketing.Contracts.Pagination;

public record BaseSearchObject
{
    public const int DefaultPage = 0;
    public const int DefaultPageSize = 10;

    /// <remarks>Stays nullable: the property initializers below only apply when the binder leaves
    /// a property alone, and minimal APIs' [AsParameters] binding assigns null for a query-string
    /// key that is absent. Nullability is also what BaseSearchObjectValidator's
    /// <c>.When(x =&gt; x.Page.HasValue)</c> guards key off. The defaults are applied on the way
    /// out, by <see cref="EffectivePage"/>, so "absent" can never mean "unpaged".</remarks>
    public int? Page { get; init; } = DefaultPage;
    public int? PageSize { get; init; } = DefaultPageSize;
    public string? FTS { get; init; }
    public bool? IsActive { get; init; }

    /// <summary>The page to actually read — the one place the default lives, so the nine
    /// repositories calling <c>ToPagedResultAsync</c> can't each coalesce it differently.</summary>
    public int EffectivePage => Page ?? DefaultPage;

    /// <inheritdoc cref="EffectivePage"/>
    public int EffectivePageSize => PageSize ?? DefaultPageSize;
}
