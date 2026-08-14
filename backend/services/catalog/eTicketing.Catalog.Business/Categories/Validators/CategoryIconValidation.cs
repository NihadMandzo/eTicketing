using eTicketing.Shared.Storage;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Categories.Validators;

/// <summary>
/// Shared icon-file rules for CategoryIconUploadRequestValidator (create + replace). Icons are
/// stored in Azure Blob Storage (category-icons container — see Category.IconBlobName), so
/// they're kept deliberately tiny: PNG only, square, capped at 1MB.
///
/// PNG-only (not PNG+SVG) is a deliberate choice, not an oversight: this app's Image.network/
/// Image.file calls (org logos, category icons) use Flutter's stock image widgets, which have
/// no SVG codec support at all — rendering an SVG-uploaded icon would just show a broken image
/// everywhere it's displayed. Adding SVG rendering support would mean pulling in the
/// flutter_svg package; PNG-only avoids that new dependency entirely.
///
/// There's deliberately no pixel-dimension cap here — icons are always rendered in a fixed-size
/// square slot regardless of source resolution, and the desktop client force-crops every upload
/// to an exact square before it's sent (see ImageCropDialog), so the byte-size cap plus the
/// square check below are the only rules that still matter server-side.
/// </summary>
internal static class CategoryIconValidation
{
    internal const long MaxPngBytes = 1 * 1024 * 1024;

    internal static bool IsWithinSizeLimit(IFormFile? file) => file is not null && file.Length <= MaxPngBytes;

    /// <summary>Format is sniffed from the actual file bytes — see
    /// ImageContentProbe.DetectFormatNameAsync for the reasoning.</summary>
    internal static async Task<bool> HasAllowedFormatAsync(IFormFile? file, CancellationToken ct) =>
        await ImageContentProbe.DetectFormatNameAsync(file, ct) == "PNG";

    /// <summary>Icons are shown at a fixed square size everywhere they're rendered (category
    /// chips/cards) — a non-square upload would get squashed/cropped unpredictably depending on
    /// the widget's BoxFit, so the aspect ratio is enforced server-side rather than left to the
    /// client to letterbox.</summary>
    internal static Task<bool> HasSquareAspectRatioAsync(IFormFile file, CancellationToken ct) =>
        ImageContentProbe.IsSquareAsync(file, ct);
}
