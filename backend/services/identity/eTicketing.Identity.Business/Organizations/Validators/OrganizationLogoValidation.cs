using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;

namespace eTicketing.Identity.Business.Organizations.Validators;

/// <summary>
/// Shared logo-file rules for CreateOrganizationRequestValidator/UpdateOrganizationRequestValidator.
/// Logos are stored as raw bytes directly in IdentityDb (no blob storage infra exists in this
/// repo — see Organization.cs), mirroring Catalog's CategoryIconValidation. Unlike category
/// icons (deliberately tiny, PNG-only), organization logos allow PNG or JPEG — both are
/// natively decodable by Flutter's stock Image widgets, so there's no SVG-style rendering gap
/// to avoid — and a more generous size, matching the 2MB cap the desktop app's settings dialog
/// already assumes for logo uploads.
/// </summary>
internal static class OrganizationLogoValidation
{
    internal static readonly string[] AllowedContentTypes = ["image/png", "image/jpeg"];
    internal const long MaxBytes = 2 * 1024 * 1024;
    internal const int MaxDimensionPx = 2000; // generous sanity cap, not a strict display-size limit

    internal static bool HasAllowedContentType(IFormFile? file) => file is not null && AllowedContentTypes.Contains(file.ContentType);

    internal static bool IsWithinSizeLimit(IFormFile? file) => file is not null && file.Length <= MaxBytes;

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
            // Genuinely corrupt/non-image content — Image.IdentifyAsync throws
            // (UnknownImageFormatException, InvalidImageContentException, ...) rather than
            // returning null for this case. This is untrusted user-upload content being
            // parsed, so a broad catch here is deliberate: any failure to identify it as a
            // valid image means invalid input, surfaced as a normal validation failure (400)
            // rather than an unhandled exception (500 via GlobalExceptionHandler).
            return false;
        }
    }
}
