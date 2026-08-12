using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;

namespace eTicketing.Catalog.Business.Categories.Validators;

/// <summary>
/// Shared icon-file rules for CreateCategoryRequestValidator/UpdateCategoryRequestValidator.
/// Icons are stored as raw bytes directly in CatalogDb (no blob storage infra exists in this
/// repo — see Category.cs), so they're kept deliberately tiny: PNG only, capped well below
/// what a ~100x100px icon actually needs.
///
/// PNG-only (not PNG+SVG) is a deliberate choice, not an oversight: this app's Image.network/
/// Image.file calls (org logos, category icons) use Flutter's stock image widgets, which have
/// no SVG codec support at all — rendering an SVG-uploaded icon would just show a broken image
/// everywhere it's displayed. Adding SVG rendering support would mean pulling in the
/// flutter_svg package; PNG-only avoids that new dependency entirely.
/// </summary>
internal static class CategoryIconValidation
{
    internal static readonly string[] AllowedContentTypes = ["image/png"];
    internal const long MaxPngBytes = 100 * 1024;
    internal const int MaxIconDimensionPx = 100;

    internal static bool HasAllowedContentType(IFormFile? file) => file is not null && AllowedContentTypes.Contains(file.ContentType);

    internal static bool IsWithinSizeLimit(IFormFile? file) => file is not null && file.Length <= MaxPngBytes;

    /// <summary>Pixel dimensions must be ≤100x100.</summary>
    internal static async Task<bool> HasValidContentAsync(IFormFile file, CancellationToken ct)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var info = await Image.IdentifyAsync(stream, ct);
            return info is not null && info.Width <= MaxIconDimensionPx && info.Height <= MaxIconDimensionPx;
        }
        catch (Exception)
        {
            // Genuinely corrupt/non-image content (e.g. a ".png"-named file that isn't
            // actually a valid PNG) — Image.IdentifyAsync throws (UnknownImageFormatException,
            // InvalidImageContentException, ...) rather than returning null for this case.
            // This is untrusted user-upload content being parsed, so a broad catch here is
            // deliberate: any failure to identify it as a valid image means invalid input,
            // surfaced as a normal validation failure (400) rather than an unhandled exception
            // (500 via GlobalExceptionHandler).
            return false;
        }
    }
}
