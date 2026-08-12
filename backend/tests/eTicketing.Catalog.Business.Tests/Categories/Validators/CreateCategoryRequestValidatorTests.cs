using eTicketing.Catalog.Business.Categories;
using eTicketing.Catalog.Business.Categories.Validators;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace eTicketing.Catalog.Business.Tests.Categories.Validators;

/// <summary>
/// Exercises the validator directly (not through CategoryService, which never re-validates —
/// FluentValidation runs in the endpoint pipeline via ValidationFilter&lt;T&gt;) — this is the
/// authoritative place these rules are actually enforced.
/// </summary>
public class CreateCategoryRequestValidatorTests
{
    private readonly CreateCategoryRequestValidator _validator = new();

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
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

    private static CreateCategoryRequest ValidRequest(IFormFile icon) => new()
    {
        Name = "Muzika",
        Description = "Opis",
        IsActive = true,
        DisplayOrder = 0,
        Icon = icon,
    };

    [Fact]
    public async Task Validate_WithValidPng100x100_Passes()
    {
        var request = ValidRequest(CreateFormFile(CreatePngBytes(100, 100)));

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithNullIcon_FailsRequired()
    {
        var request = ValidRequest(null!);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Ikona kategorije je obavezna.");
    }

    [Fact]
    public async Task Validate_WithNonPngContentType_Fails()
    {
        var request = ValidRequest(CreateFormFile(CreatePngBytes(50, 50), contentType: "image/jpeg"));

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Dozvoljeni format ikone je PNG.");
    }

    [Fact]
    public async Task Validate_WithSvgContentType_Fails()
    {
        var svgBytes = System.Text.Encoding.UTF8.GetBytes("<svg></svg>");
        var request = ValidRequest(CreateFormFile(svgBytes, contentType: "image/svg+xml"));

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithOversizedIcon_Fails()
    {
        // 101 KB of raw bytes is well over the 100 KB cap regardless of what codec-level
        // validation would say about it — the size rule runs before content validation.
        var oversized = new byte[101 * 1024];
        var request = ValidRequest(CreateFormFile(oversized));

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Ikona je prevelika (maks. 100 KB).");
    }

    [Fact]
    public async Task Validate_WithOversizedDimensions_Fails()
    {
        var request = ValidRequest(CreateFormFile(CreatePngBytes(101, 101)));

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Ikona ne zadovoljava ograničenja (maks. 100x100px).");
    }

    [Fact]
    public async Task Validate_WithCorruptContent_FailsWithoutThrowing()
    {
        // Regression test: Image.IdentifyAsync throws (rather than returning null) for content
        // that carries a PNG content-type but isn't actually decodable — CategoryIconValidation
        // must catch that and report it as a normal validation failure, not let it propagate as
        // an unhandled exception (surfaced as a 500 via GlobalExceptionHandler otherwise).
        var corrupt = "\x89PNG\r\n\x1a\nnot a real png"u8.ToArray();
        var request = ValidRequest(CreateFormFile(corrupt));

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Ikona ne zadovoljava ograničenja (maks. 100x100px).");
    }

    [Fact]
    public async Task Validate_WithEmptyName_Fails()
    {
        var request = ValidRequest(CreateFormFile(CreatePngBytes(50, 50)));
        request.Name = "";

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }
}
