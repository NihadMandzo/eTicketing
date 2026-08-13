using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;

namespace eTicketing.Identity.Business.Organizations.Validators;

/// <summary>
/// Shared logo-file rules for OrganizationLogoUploadRequestValidator (create + replace). Logos
/// are stored in Azure Blob Storage (organization-logos container — see
/// Organization.LogoBlobName), mirroring Catalog's CategoryIconValidation. Unlike category icons
/// (deliberately tiny, PNG-only), organization logos allow PNG or JPEG — both are natively
/// decodable by Flutter's stock Image widgets, so there's no SVG-style rendering gap to avoid.
/// </summary>
internal static class OrganizationLogoValidation
{
    internal const long MaxBytes = 1 * 1024 * 1024;
    internal const int MaxDimensionPx = 2000; // generous sanity cap, not a strict display-size limit

    internal static bool IsWithinSizeLimit(IFormFile? file) => file is not null && file.Length <= MaxBytes;

    /// <summary>Format is sniffed from the actual file bytes, not the request's Content-Type
    /// header — see CategoryIconValidation.HasAllowedFormatAsync for the same reasoning.</summary>
    internal static async Task<bool> HasAllowedFormatAsync(IFormFile? file, CancellationToken ct)
    {
        var format = await DetectFormatNameAsync(file, ct);
        return format is "PNG" or "JPEG";
    }

    internal static async Task<bool> HasValidContentAsync(IFormFile file, CancellationToken ct)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var info = await Image.IdentifyAsync(stream, ct);
            return info is not null && info.Width <= MaxDimensionPx && info.Height <= MaxDimensionPx;
        }
        catch (Exception)
        {
            // Same reasoning as HasAllowedFormatAsync below.
            return false;
        }
    }

    /// <summary>Logos are shown in a fixed square avatar (org cards, header, settings dialog) —
    /// a non-square upload would get squashed/cropped unpredictably, so the aspect ratio is
    /// enforced server-side rather than left to the client to letterbox. Mirrors
    /// CategoryIconValidation.HasSquareAspectRatioAsync.</summary>
    internal static async Task<bool> HasSquareAspectRatioAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null) return false;

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

    /// <summary>Blob file extension for a file already known to have passed HasAllowedFormatAsync
    /// — called by OrganizationService at upload time, after validation, to pick the right
    /// key ("{id}-{slug}.png" vs "...jpg").</summary>
    internal static async Task<string> DetectExtensionAsync(IFormFile file, CancellationToken ct) =>
        await DetectFormatNameAsync(file, ct) == "PNG" ? "png" : "jpg";

    private static async Task<string?> DetectFormatNameAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null) return null;

        try
        {
            await using var stream = file.OpenReadStream();
            var format = await Image.DetectFormatAsync(stream, ct);
            return format?.Name;
        }
        catch (Exception)
        {
            // Genuinely corrupt/non-image content — format detection throws for this rather than
            // returning null. Untrusted user-upload content, so a broad catch is deliberate: any
            // failure to identify it means invalid input, surfaced as a normal validation failure
            // (400) rather than an unhandled exception (500 via GlobalExceptionHandler).
            return null;
        }
    }
}
