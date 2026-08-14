using eTicketing.Shared.Storage;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Identity.Business.Organizations.Validators;

/// <summary>
/// Shared logo-file rules for OrganizationLogoUploadRequestValidator (create + replace). Logos
/// are stored in Azure Blob Storage (organization-logos container — see
/// Organization.LogoBlobName), mirroring Catalog's CategoryIconValidation. Unlike category icons
/// (deliberately tiny, PNG-only), organization logos allow PNG or JPEG — both are natively
/// decodable by Flutter's stock Image widgets, so there's no SVG-style rendering gap to avoid.
///
/// There's deliberately no pixel-dimension cap here — logos are always rendered in a fixed-size
/// square avatar regardless of source resolution, and the desktop client force-crops every
/// upload to an exact square before it's sent (see ImageCropDialog), so the byte-size cap plus
/// the square check below are the only rules that still matter server-side.
/// </summary>
internal static class OrganizationLogoValidation
{
    internal const long MaxBytes = 1 * 1024 * 1024;

    internal static bool IsWithinSizeLimit(IFormFile? file) => file is not null && file.Length <= MaxBytes;

    /// <summary>Format is sniffed from the actual file bytes — see
    /// ImageContentProbe.DetectFormatNameAsync for the reasoning.</summary>
    internal static async Task<bool> HasAllowedFormatAsync(IFormFile? file, CancellationToken ct)
    {
        var format = await ImageContentProbe.DetectFormatNameAsync(file, ct);
        return format is "PNG" or "JPEG";
    }

    /// <summary>Logos are shown in a fixed square avatar (org cards, header, settings dialog) —
    /// a non-square upload would get squashed/cropped unpredictably, so the aspect ratio is
    /// enforced server-side rather than left to the client to letterbox. Mirrors
    /// CategoryIconValidation.HasSquareAspectRatioAsync.</summary>
    internal static Task<bool> HasSquareAspectRatioAsync(IFormFile? file, CancellationToken ct) =>
        ImageContentProbe.IsSquareAsync(file, ct);

    /// <summary>Blob file extension for a file already known to have passed HasAllowedFormatAsync
    /// — called by OrganizationService at upload time, after validation, to pick the right
    /// key ("{id}-{slug}.png" vs "...jpg").</summary>
    internal static async Task<string> DetectExtensionAsync(IFormFile file, CancellationToken ct) =>
        await ImageContentProbe.DetectFormatNameAsync(file, ct) == "PNG" ? "png" : "jpg";
}
