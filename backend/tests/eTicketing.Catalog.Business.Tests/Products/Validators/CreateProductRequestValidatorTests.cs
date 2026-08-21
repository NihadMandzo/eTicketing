using eTicketing.Catalog.Business.Products;
using eTicketing.Catalog.Business.Products.Validators;
using eTicketing.Contracts.Persistence;
using FluentAssertions;

namespace eTicketing.Catalog.Business.Tests.Products.Validators;

/// <summary>
/// Exercises the validator directly (not through ProductService, which never re-validates
/// field-level rules — FluentValidation runs in the endpoint pipeline via ValidationFilter&lt;T&gt;)
/// — this is the authoritative place the Latitude/Longitude/City "must place an exact pin" rules
/// are actually enforced. Cross-entity rules (category exists, Date required-iff-SingleOccurrence)
/// live in ProductService.ValidateAsync instead — see ProductServiceTests.
/// </summary>
public class CreateProductRequestValidatorTests
{
    private readonly CreateProductRequestValidator _validator = new();

    private static UpsertProductRequest ValidRequest() => new()
    {
        Name = "Ljetni Festival",
        Description = "Opis",
        Date = DateTime.UtcNow.AddDays(5),
        CategoryId = 1,
        Latitude = 43.8563,
        Longitude = 18.4131,
        City = City.Sarajevo,
    };

    [Fact]
    public async Task Validate_WithValidRequest_Passes()
    {
        var result = await _validator.ValidateAsync(ValidRequest());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithMissingLatitude_Fails()
    {
        var request = ValidRequest() with { Latitude = null };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpsertProductRequest.Latitude));
    }

    [Fact]
    public async Task Validate_WithMissingLongitude_Fails()
    {
        var request = ValidRequest() with { Longitude = null };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpsertProductRequest.Longitude));
    }

    [Fact]
    public async Task Validate_WithMissingCity_Fails()
    {
        var request = ValidRequest() with { City = null };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpsertProductRequest.City));
    }

    [Theory]
    [InlineData(90.0001)]
    [InlineData(-90.0001)]
    public async Task Validate_WithLatitudeOutOfRange_Fails(double latitude)
    {
        var request = ValidRequest() with { Latitude = latitude };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpsertProductRequest.Latitude));
    }

    [Theory]
    [InlineData(180.0001)]
    [InlineData(-180.0001)]
    public async Task Validate_WithLongitudeOutOfRange_Fails(double longitude)
    {
        var request = ValidRequest() with { Longitude = longitude };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpsertProductRequest.Longitude));
    }

    [Theory]
    [InlineData(90)]
    [InlineData(-90)]
    public async Task Validate_WithLatitudeAtBoundary_Passes(double latitude)
    {
        var request = ValidRequest() with { Latitude = latitude };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithInvalidCityEnumValue_Fails()
    {
        var request = ValidRequest() with { City = (City)999 };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpsertProductRequest.City));
    }
}
