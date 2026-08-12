using eTicketing.Identity.Business.Organizations;
using eTicketing.Identity.Business.Organizations.Validators;
using eTicketing.Identity.Data.Enums;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace eTicketing.Identity.Business.Tests.Organizations.Validators;

/// <summary>Covers only the new Logo rules added to CreateOrganizationRequestValidator — the
/// pre-existing Name/Email/Admin* rules aren't part of this change and already have their own
/// coverage path via OrganizationServiceTests.</summary>
public class CreateOrganizationRequestValidatorLogoTests
{
    private readonly CreateOrganizationRequestValidator _validator = new();

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
        return new FormFile(stream, 0, stream.Length, "Logo", "logo.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private static CreateOrganizationRequest ValidRequest(IFormFile? logo) => new()
    {
        Name = "Acme Events",
        Description = "Desc",
        Address = "123 Main St",
        PhoneNumber = "+387 61 000 000",
        Email = "info@acme.example.com",
        AdminFirstName = "Alice",
        AdminLastName = "Admin",
        AdminEmail = "alice@acme.example.com",
        AdminUsername = "aliceadmin",
        AdminPassword = "SuperSecret123",
        AdminRole = RoleType.OrganizationSuperAdmin,
        Logo = logo,
    };

    [Fact]
    public async Task Logo_Absent_DoesNotFail()
    {
        // Unlike Category's icon, an organization logo is optional on create.
        var result = await _validator.TestValidateAsync(ValidRequest(null));

        result.ShouldNotHaveValidationErrorFor(x => x.Logo);
    }

    [Fact]
    public async Task Logo_ValidPng_DoesNotFail()
    {
        var result = await _validator.TestValidateAsync(ValidRequest(CreateFormFile(CreatePngBytes(200, 200))));

        result.ShouldNotHaveValidationErrorFor(x => x.Logo);
    }

    [Fact]
    public async Task Logo_ValidJpeg_DoesNotFail()
    {
        var stream = new MemoryStream();
        using (var image = new Image<Rgba32>(200, 200))
        {
            image.SaveAsJpeg(stream);
        }
        var jpeg = new FormFile(stream, 0, stream.Length, "Logo", "logo.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg",
        };

        var result = await _validator.TestValidateAsync(ValidRequest(jpeg));

        result.ShouldNotHaveValidationErrorFor(x => x.Logo);
    }

    [Fact]
    public async Task Logo_DisallowedContentType_Fails()
    {
        var svgBytes = System.Text.Encoding.UTF8.GetBytes("<svg></svg>");

        var result = await _validator.TestValidateAsync(ValidRequest(CreateFormFile(svgBytes, "image/svg+xml")));

        result.ShouldHaveValidationErrorFor(x => x.Logo)
            .WithErrorMessage("Dozvoljeni formati loga su PNG i JPEG.");
    }

    [Fact]
    public async Task Logo_OverTwoMegabytes_Fails()
    {
        var oversized = new byte[(2 * 1024 * 1024) + 1];

        var result = await _validator.TestValidateAsync(ValidRequest(CreateFormFile(oversized)));

        result.ShouldHaveValidationErrorFor(x => x.Logo)
            .WithErrorMessage("Logo može biti maksimalno 2MB.");
    }

    [Fact]
    public async Task Logo_CorruptContent_FailsWithoutThrowing()
    {
        // Regression test: Image.IdentifyAsync throws (rather than returning null) for content
        // that merely looks like an image but isn't decodable — OrganizationLogoValidation must
        // catch that and report it as a normal validation failure, not let it become an
        // unhandled exception (surfaced as a 500 via GlobalExceptionHandler otherwise).
        var corrupt = "\x89PNG\r\n\x1a\nnot a real png"u8.ToArray();

        var result = await _validator.TestValidateAsync(ValidRequest(CreateFormFile(corrupt)));

        result.ShouldHaveValidationErrorFor(x => x.Logo)
            .WithErrorMessage("Logo nije validna slika ili je prevelike rezolucije.");
    }

    [Fact]
    public async Task Logo_OversizedDimensions_Fails()
    {
        var result = await _validator.TestValidateAsync(ValidRequest(CreateFormFile(CreatePngBytes(2001, 2001))));

        result.ShouldHaveValidationErrorFor(x => x.Logo)
            .WithErrorMessage("Logo nije validna slika ili je prevelike rezolucije.");
    }
}
