using eTicketing.Catalog.Business.Categories;
using eTicketing.Catalog.Business.Categories.Validators;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace eTicketing.Catalog.Business.Tests.Categories.Validators;

public class CategoryIconUploadRequestValidatorTests
{
    private readonly CategoryIconUploadRequestValidator _validator = new();

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static byte[] CreateJpegBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    private static IFormFile CreateFormFile(byte[] bytes, string contentType = "image/png")
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, "Icon", "icon.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private static CategoryIconUploadRequest Request(IFormFile? icon) => new() { Icon = icon! };

    [Fact]
    public async Task Validate_WithValidPng100x100_Passes()
    {
        var result = await _validator.ValidateAsync(Request(CreateFormFile(CreatePngBytes(100, 100))));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithNullIcon_FailsRequired()
    {
        var result = await _validator.ValidateAsync(Request(null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Ikona kategorije je obavezna.");
    }

    [Fact]
    public async Task Validate_WithRealPngBytesButWrongContentTypeHeader_Passes()
    {
        // Regression test for the fix this task made: the desktop client's Dio multipart upload
        // used to send a real PNG file with a wrong/missing Content-Type header (defaults to
        // application/octet-stream), which the OLD header-trusting validator rejected. Format is
        // now sniffed from the actual bytes, so this must pass regardless of the header.
        var request = Request(CreateFormFile(CreatePngBytes(50, 50), contentType: "application/octet-stream"));

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithRealJpegBytesEvenWithPngContentTypeHeader_Fails()
    {
        // The inverse of the above: a real non-PNG image with a PNG-claiming header must still
        // fail — the header is never trusted either way.
        var request = Request(CreateFormFile(CreateJpegBytes(50, 50), contentType: "image/png"));

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Dozvoljeni format ikone je PNG.");
    }

    [Fact]
    public async Task Validate_WithSvgContentType_Fails()
    {
        var svgBytes = System.Text.Encoding.UTF8.GetBytes("<svg></svg>");
        var request = Request(CreateFormFile(svgBytes, contentType: "image/svg+xml"));

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithOversizedIcon_Fails()
    {
        // 1MB + 1 byte of raw bytes is well over the cap regardless of what codec-level
        // validation would say about it — the size rule runs before content validation.
        var oversized = new byte[(1 * 1024 * 1024) + 1];
        var request = Request(CreateFormFile(oversized));

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Ikona može biti maksimalno 1MB.");
    }

    [Fact]
    public async Task Validate_WithNonSquareDimensions_Fails()
    {
        // 80x40 is well under the 1MB size cap, so this isolates the aspect-ratio rule from the
        // size rule above.
        var request = Request(CreateFormFile(CreatePngBytes(80, 40)));

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Ikona mora biti kvadratna (omjer 1:1).");
    }

    [Fact]
    public async Task Validate_WithSquareDimensions_PassesAspectRatioRule()
    {
        var result = await _validator.ValidateAsync(Request(CreateFormFile(CreatePngBytes(64, 64))));

        result.Errors.Should().NotContain(e => e.ErrorMessage == "Ikona mora biti kvadratna (omjer 1:1).");
    }

    [Fact]
    public async Task Validate_WithCorruptContent_FailsWithoutThrowing()
    {
        // Regression test: Image.DetectFormatAsync/IdentifyAsync throw (rather than returning
        // null) for content that carries a PNG-like signature but isn't actually decodable —
        // CategoryIconValidation must catch that and report it as a normal validation failure,
        // not let it propagate as an unhandled exception (500 via GlobalExceptionHandler).
        var corrupt = "\x89PNG\r\n\x1a\nnot a real png"u8.ToArray();
        var request = Request(CreateFormFile(corrupt));

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }
}
