using System.Text.RegularExpressions;

namespace eTicketing.Shared.Storage;

/// <summary>
/// Shared blob-key convention: "{entityId}-{slug(name)}.{ext}" (e.g. "1-muzika.png"). The id
/// prefix guarantees uniqueness (entity Names aren't unique-constrained); the slug keeps the key
/// human-readable and tied to the entity it belongs to, per the "name of the picture should be
/// the name of the category/organization" requirement.
/// </summary>
public static partial class BlobNaming
{
    /// <summary>Lowercases, transliterates the Bosnian-specific diacritics, and collapses
    /// everything else into single dashes so the result is a safe blob-name segment.</summary>
    public static string Slugify(string name)
    {
        var lowered = name.Trim().ToLowerInvariant()
            .Replace('č', 'c').Replace('ć', 'c')
            .Replace('š', 's')
            .Replace('ž', 'z')
            .Replace('đ', 'd');

        var slug = NonAlphanumeric().Replace(lowered, "-").Trim('-');
        return slug.Length == 0 ? "icon" : slug;
    }

    public static string BuildBlobName(object entityId, string name, string extension) =>
        $"{entityId}-{Slugify(name)}.{extension}";

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
