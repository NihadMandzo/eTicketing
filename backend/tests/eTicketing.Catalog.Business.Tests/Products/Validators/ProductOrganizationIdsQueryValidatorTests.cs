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
