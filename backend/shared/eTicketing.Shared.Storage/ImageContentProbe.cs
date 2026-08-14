using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;

namespace eTicketing.Shared.Storage;

/// <summary>
/// Shared image-content probes for upload validators (CategoryIconValidation,
/// OrganizationLogoValidation) — format sniffing and square-aspect-ratio checking decode the
/// actual file bytes, never trusting client-supplied metadata (Content-Type header, filename
/// extension), and both validators need the identical logic. Corrupt/undecodable content
/// resolves to null/false rather than throwing: untrusted user-upload content, so any decode
/// failure is surfaced as a normal validation failure (400) rather than an unhandled exception
/// (500 via GlobalExceptionHandler).
/// </summary>
public static class ImageContentProbe
{
    /// <summary>Format is sniffed from the actual file bytes, not the request's Content-Type
    /// header — the header is client-supplied and unreliable (e.g. Dio-based multipart clients
    /// commonly default to application/octet-stream for a real image file unless the caller sets
    /// contentType explicitly), so trusting it would reject genuinely valid uploads.</summary>
    public static async Task<string?> DetectFormatNameAsync(IFormFile? file, CancellationToken ct)
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
            // returning null.
            return null;
        }
    }

    public static async Task<bool> IsSquareAsync(IFormFile? file, CancellationToken ct)
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
            // Same reasoning as DetectFormatNameAsync above.
            return false;
        }
    }
}
