using eTicketing.Identity.Business.Organizations;
using eTicketing.Identity.Business.Organizations.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace eTicketing.Identity.Business.Tests.Organizations.Validators;

/// <summary>Covers OrganizationLogoUploadRequestValidator — logos are no longer part of
/// Create/UpdateOrganizationRequest at all (see the dedicated POST/PUT /organizations/{id}/logo
/// endpoints); this is the sole place these rules are enforced now.</summary>
public class OrganizationLogoUploadRequestValidatorTests
{
    private readonly OrganizationLogoUploadRequestValidator _validator = new();

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
        return new FormFile(stream, 0, stream.Length, "Logo", "logo.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    [Fact]
    public async Task Logo_Absent_FailsRequired()
    {
        var result = await _validator.TestValidateAsync(new OrganizationLogoUploadRequest { Logo = null! });

        result.ShouldHaveValidationErrorFor(x => x.Logo)
            .WithErrorMessage("Logo organizacije je obavezan.");
    }

    [Fact]
    public async Task Logo_ValidPng_DoesNotFail()
    {
        var result = await _validator.TestValidateAsync(new OrganizationLogoUploadRequest { Logo = CreateFormFile(CreatePngBytes(200, 200)) });

        result.ShouldNotHaveValidationErrorFor(x => x.Logo);
    }

    [Fact]
    public async Task Logo_ValidJpeg_DoesNotFail()
    {
        var jpeg = CreateFormFile(CreateJpegBytes(200, 200), contentType: "image/jpeg");

        var result = await _validator.TestValidateAsync(new OrganizationLogoUploadRequest { Logo = jpeg });

        result.ShouldNotHaveValidationErrorFor(x => x.Logo);
    }

    [Fact]
    public async Task Logo_RealPngBytesWithWrongContentTypeHeader_DoesNotFail()
    {
        // Regression test for the fix this task made: the desktop client's Dio multipart upload
        // used to send a real image file with a wrong/missing Content-Type header, which the OLD
        // header-trusting validator rejected. Format is now sniffed from the actual bytes.
        var request = new OrganizationLogoUploadRequest
        {
            Logo = CreateFormFile(CreatePngBytes(100, 100), contentType: "application/octet-stream"),
        };

        var result = await _validator.TestValidateAsync(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Logo);
    }

    [Fact]
    public async Task Logo_DisallowedContentType_Fails()
    {
        var svgBytes = System.Text.Encoding.UTF8.GetBytes("<svg></svg>");

        var result = await _validator.TestValidateAsync(new OrganizationLogoUploadRequest
        {
            Logo = CreateFormFile(svgBytes, "image/svg+xml"),
        });

        result.ShouldHaveValidationErrorFor(x => x.Logo)
            .WithErrorMessage("Dozvoljeni formati loga su PNG i JPEG.");
    }

    [Fact]
    public async Task Logo_OverOneMegabyte_Fails()
    {
        var oversized = new byte[(1 * 1024 * 1024) + 1];

        var result = await _validator.TestValidateAsync(new OrganizationLogoUploadRequest { Logo = CreateFormFile(oversized) });

        result.ShouldHaveValidationErrorFor(x => x.Logo)
            .WithErrorMessage("Logo može biti maksimalno 1MB.");
    }

    [Fact]
    public async Task Logo_NonSquareDimensions_Fails()
    {
        // 300x150 is well under the 1MB size cap, so this isolates the aspect-ratio rule from
        // the size rule above.
        var result = await _validator.TestValidateAsync(new OrganizationLogoUploadRequest
        {
            Logo = CreateFormFile(CreatePngBytes(300, 150)),
        });

        result.ShouldHaveValidationErrorFor(x => x.Logo)
            .WithErrorMessage("Logo mora biti kvadratan (omjer 1:1).");
    }

    [Fact]
    public async Task Logo_SquareDimensions_PassesAspectRatioRule()
    {
        var result = await _validator.TestValidateAsync(new OrganizationLogoUploadRequest
        {
            Logo = CreateFormFile(CreatePngBytes(300, 300)),
        });

        result.Errors.Should().NotContain(e => e.ErrorMessage == "Logo mora biti kvadratan (omjer 1:1).");
    }

    [Fact]
    public async Task Logo_CorruptContent_FailsWithoutThrowing()
    {
        // Regression test: content detection throws (rather than returning null) for content
        // that carries a PNG-like signature but isn't actually decodable — the format sniff
        // (header bytes only) passes, but the square-aspect-ratio check (full decode) must catch
        // the failure and report it as a normal validation failure, not let it become an
        // unhandled exception (surfaced as a 500 via GlobalExceptionHandler otherwise).
        var corrupt = "\x89PNG\r\n\x1a\nnot a real png"u8.ToArray();

        var result = await _validator.TestValidateAsync(new OrganizationLogoUploadRequest { Logo = CreateFormFile(corrupt) });

        result.ShouldHaveValidationErrorFor(x => x.Logo)
            .WithErrorMessage("Logo mora biti kvadratan (omjer 1:1).");
    }
}
