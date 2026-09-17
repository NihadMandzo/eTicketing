using eTicketing.Catalog.Business.Products;
using eTicketing.Catalog.Business.Products.Validators;
using FluentAssertions;

namespace eTicketing.Catalog.Business.Tests.Products.Validators;

/// <summary>
/// GET /products/organization-ids used to parse its comma-joined categoryIds inline in the
/// endpoint handler and silently drop anything unparseable, so "?categoryIds=1,abc" filtered by
/// category 1 and the caller never learned half its filter had been ignored.
/// </summary>
public class ProductOrganizationIdsQueryValidatorTests
{
    private readonly ProductOrganizationIdsQueryValidator _validator = new();

    [Theory]
    [InlineData("1")]
    [InlineData("1,2,3")]
    [InlineData(" 1 , 2 ")]
    [InlineData(null)]
    [InlineData("")]
    public async Task Validate_WithParseableOrAbsentIds_Passes(string? categoryIds)
    {
        var result = await _validator.ValidateAsync(new ProductOrganizationIdsQuery { CategoryIds = categoryIds });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithAWhitespaceOnlyValue_PassesAsNoFilter()
    {
        var result = await _validator.ValidateAsync(new ProductOrganizationIdsQuery { CategoryIds = "   " });

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(",")]
    [InlineData(" , ")]
    [InlineData("1,,2")]
    [InlineData("1,2,")]
    [InlineData(",1")]
    public async Task Validate_WithAnEmptyToken_Fails(string categoryIds)
    {
        // RemoveEmptyEntries used to drop these before validation, so "," was a non-blank value that
        // parsed down to no ids and came back as an empty 200 instead of a 400.
        var result = await _validator.ValidateAsync(new ProductOrganizationIdsQuery { CategoryIds = categoryIds });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(ProductOrganizationIdsQuery.CategoryIds));
    }

    [Fact]
    public async Task Validate_WithExactlyTheCap_Passes()
    {
        var atCap = string.Join(',', Enumerable.Range(1, ProductOrganizationIdsQuery.MaxCategoryIds));

        var result = await _validator.ValidateAsync(new ProductOrganizationIdsQuery { CategoryIds = atCap });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Tokens_KeepsEmptyTokensSoTheValidatorCanSeeThem()
    {
        new ProductOrganizationIdsQuery { CategoryIds = "1,,2" }.Tokens().Should().Equal("1", "", "2");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Tokens_WhenAbsentOrBlank_IsEmpty(string? categoryIds)
    {
        new ProductOrganizationIdsQuery { CategoryIds = categoryIds }.Tokens().Should().BeEmpty();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1,abc")]
    [InlineData("1,-2")]
    [InlineData("0")]
    [InlineData("1.5")]
    public async Task Validate_WithAnUnparseableOrNonPositiveId_Fails(string categoryIds)
    {
        var result = await _validator.ValidateAsync(new ProductOrganizationIdsQuery { CategoryIds = categoryIds });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithMoreIdsThanTheCap_Fails()
    {
        var tooMany = string.Join(',', Enumerable.Range(1, ProductOrganizationIdsQuery.MaxCategoryIds + 1));

        var result = await _validator.ValidateAsync(new ProductOrganizationIdsQuery { CategoryIds = tooMany });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ParsedCategoryIds_DeduplicatesAndPreservesEveryDistinctId()
    {
        var query = new ProductOrganizationIdsQuery { CategoryIds = "3,1,3,2" };

        query.ParsedCategoryIds.Should().BeEquivalentTo([3, 1, 2]);
    }

    [Fact]
    public void ParsedCategoryIds_WhenAbsent_IsEmpty()
    {
        new ProductOrganizationIdsQuery { CategoryIds = null }.ParsedCategoryIds.Should().BeEmpty();
    }
}
