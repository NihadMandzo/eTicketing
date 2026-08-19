using eTicketing.Shared.Storage;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Catalog.Business.Products.Validators;

/// <summary>
/// Shared image-file rules for ProductImageUploadRequestValidator. Product photos are stored in
/// Azure Blob Storage (product-images container — see ProductImage.BlobName), mirroring
/// CategoryIconValidation/OrganizationLogoValidation. Unlike those two (fixed-size avatars,
/// square-cropped client-side), a product gallery photo is rendered at varying aspect ratios in
/// cards/carousels, so there's deliberately no square-aspect check here — only format and size.
/// MaxCount (5) is enforced in ProductService.UploadImageAsync, not here, since it needs a DB
/// lookup (how many images the product already has) that a stateless FluentValidation rule can't
/// perform.
/// </summary>
internal static class ProductImageValidation
{
    internal const long MaxBytes = 2 * 1024 * 1024;
    internal const int MaxCount = 5;

    internal static bool IsWithinSizeLimit(IFormFile? file) => file is not null && file.Length <= MaxBytes;

    /// <summary>Format is sniffed from the actual file bytes — see
    /// ImageContentProbe.DetectFormatNameAsync for the reasoning.</summary>
    internal static async Task<bool> HasAllowedFormatAsync(IFormFile? file, CancellationToken ct)
    {
        var format = await ImageContentProbe.DetectFormatNameAsync(file, ct);
        return format is "PNG" or "JPEG";
    }

    /// <summary>Blob file extension for a file already known to have passed HasAllowedFormatAsync
    /// — called by ProductService at upload time, after validation, to pick the right key
    /// ("{imageId}-{slug}.png" vs "...jpg").</summary>
    internal static async Task<string> DetectExtensionAsync(IFormFile file, CancellationToken ct) =>
        await ImageContentProbe.DetectFormatNameAsync(file, ct) == "PNG" ? "png" : "jpg";
}
