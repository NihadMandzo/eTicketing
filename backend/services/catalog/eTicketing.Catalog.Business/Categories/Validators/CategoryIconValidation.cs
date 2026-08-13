using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;

namespace eTicketing.Catalog.Business.Categories.Validators;

/// <summary>
/// Shared icon-file rules for CategoryIconUploadRequestValidator (create + replace). Icons are
/// stored in Azure Blob Storage (category-icons container — see Category.IconBlobName), so
/// they're kept deliberately tiny: PNG only, capped well below what a ~100x100px icon actually
/// needs.
///
/// PNG-only (not PNG+SVG) is a deliberate choice, not an oversight: this app's Image.network/
/// Image.file calls (org logos, category icons) use Flutter's stock image widgets, which have
/// no SVG codec support at all — rendering an SVG-uploaded icon would just show a broken image
/// everywhere it's displayed. Adding SVG rendering support would mean pulling in the
/// flutter_svg package; PNG-only avoids that new dependency entirely.
/// </summary>
internal static class CategoryIconValidation
{
    internal const long MaxPngBytes = 100 * 1024;
    internal const int MaxIconDimensionPx = 100;

    internal static bool IsWithinSizeLimit(IFormFile? file) => file is not null && file.Length <= MaxPngBytes;

    /// <summary>Format is sniffed from the actual file bytes, not the request's Content-Type
    /// header — the header is client-supplied and unreliable (e.g. Dio-based multipart clients
    /// commonly default to application/octet-stream for a real PNG file unless the caller sets
    /// contentType explicitly), so trusting it would reject genuinely valid uploads.</summary>
    internal static async Task<bool> HasAllowedFormatAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null) return false;

        try
        {
            await using var stream = file.OpenReadStream();
            var format = await Image.DetectFormatAsync(stream, ct);
            return format is not null && format.Name == "PNG";
        }
        catch (Exception)
        {
            // Genuinely corrupt/non-image content — format detection throws for this rather than
            // returning null. Untrusted user-upload content, so a broad catch is deliberate: any
            // failure to identify it means invalid input, surfaced as a normal validation failure
            // (400) rather than an unhandled exception (500 via GlobalExceptionHandler).
            return false;
        }
    }

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
            // Same reasoning as HasAllowedFormatAsync above.
            return false;
        }
    }

    /// <summary>Icons are shown at a fixed square size everywhere they're rendered (category
    /// chips/cards) — a non-square upload would get squashed/cropped unpredictably depending on
    /// the widget's BoxFit, so the aspect ratio is enforced server-side rather than left to the
    /// client to letterbox.</summary>
    internal static async Task<bool> HasSquareAspectRatioAsync(IFormFile file, CancellationToken ct)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var info = await Image.IdentifyAsync(stream, ct);
            return info is not null && info.Width == info.Height;
        }
        catch (Exception)
        {
            // Same reasoning as HasAllowedFormatAsync above.
            return false;
        }
    }
}
