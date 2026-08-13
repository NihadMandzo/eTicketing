using eTicketing.Catalog.Business.Categories;
using eTicketing.Catalog.Business.Categories.Validators;
using FluentAssertions;

namespace eTicketing.Catalog.Business.Tests.Categories.Validators;

/// <summary>
/// Exercises the validator directly (not through CategoryService, which never re-validates —
/// FluentValidation runs in the endpoint pipeline via ValidationFilter&lt;T&gt;) — this is the
/// authoritative place these rules are actually enforced. Icon rules moved to
/// CategoryIconUploadRequestValidatorTests.cs — icons are no longer part of this request at all
/// (see CategoryIconUploadRequest / the dedicated icon endpoints).
/// </summary>
public class CreateCategoryRequestValidatorTests
{
    private readonly CreateCategoryRequestValidator _validator = new();

    private static CreateCategoryRequest ValidRequest() => new()
    {
        Name = "Muzika",
        Description = "Opis",
        IsActive = true,
        DisplayOrder = 0,
    };

    [Fact]
    public async Task Validate_WithValidRequest_Passes()
    {
        var result = await _validator.ValidateAsync(ValidRequest());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithEmptyName_Fails()
    {
        var request = ValidRequest() with { Name = "" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithTooShortName_Fails()
    {
        var request = ValidRequest() with { Name = "A" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithTooLongName_Fails()
    {
        var request = ValidRequest() with { Name = new string('a', 101) };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithTooLongDescription_Fails()
    {
        var request = ValidRequest() with { Description = new string('a', 501) };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithNegativeDisplayOrder_Fails()
    {
        var request = ValidRequest() with { DisplayOrder = -1 };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }
}
