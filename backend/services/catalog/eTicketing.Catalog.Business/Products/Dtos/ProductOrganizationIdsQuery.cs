namespace eTicketing.Catalog.Business.Products;

/// <summary>
/// Query for GET /products/organization-ids. <c>categoryIds</c> is a single comma-joined query
/// param ("?categoryIds=1,2,3"), deliberately not ASP.NET Core's repeated-key array binding —
/// that is how the desktop client builds this call, so the key name and the comma format are both
/// load-bearing.
/// </summary>
/// <remarks>The parsing lives here rather than in the endpoint handler (which used to split,
/// parse, filter and de-duplicate inline) so the handler stays bind → call → map, and so the
/// format can be validated before any of it runs.</remarks>
public sealed record ProductOrganizationIdsQuery
{
    /// <summary>Upper bound on the id list, mirroring the internal by-ids caps elsewhere: the
    /// value arrives as free text, so this is what stands between a caller bug and an unbounded
    /// IN (...) clause.</summary>
    public const int MaxCategoryIds = 100;

    public string? CategoryIds { get; init; }

    /// <summary>The ids as the repository wants them. Empty when the parameter is absent or blank
    /// — the service treats that as "no filter selected" and answers with an empty list.</summary>
    public IReadOnlyList<int> ParsedCategoryIds =>
        Tokens()
            .Select(token => int.TryParse(token, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

    /// <summary>Raw tokens, before parsing — the validator reports on these so a malformed id is
    /// a 400 rather than being silently dropped from the filter.
    ///
    /// <para>Empty tokens are kept, not removed. Removing them let "?categoryIds=," pass validation
    /// as a non-blank value that parsed down to no ids at all, and "1,,2" pass as if it were
    /// "1,2" — a malformed filter answered with a 200. An absent or blank parameter is still no
    /// tokens, which is "no filter selected".</para></summary>
    public IReadOnlyList<string> Tokens() =>
        string.IsNullOrWhiteSpace(CategoryIds)
            ? []
            : CategoryIds.Split(',', StringSplitOptions.TrimEntries);
}
