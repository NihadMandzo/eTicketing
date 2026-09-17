using eTicketing.Ticketing.Business.TicketPrint;
using eTicketing.Ticketing.Business.TicketPrint.Validators;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.TicketPrint.Validators;

/// <summary>
/// Both routes previously bound a bare Guid with no validator at all, so Guid.Empty bound happily
/// and travelled as far as a cross-service HTTP call to Catalog before anything rejected it.
/// </summary>
public class TicketPrintQueryValidatorTests
{
    private readonly TicketPrintOptionsQueryValidator _optionsValidator = new();
    private readonly TicketPrintLatestQueryValidator _latestValidator = new();

    [Fact]
    public async Task ValidateOptions_WithARealProductId_Passes()
    {
        var result = await _optionsValidator.ValidateAsync(
            new TicketPrintOptionsQuery { ProductId = Guid.NewGuid() });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateOptions_WithAnEmptyProductId_Fails()
    {
        var result = await _optionsValidator.ValidateAsync(
            new TicketPrintOptionsQuery { ProductId = Guid.Empty });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(TicketPrintOptionsQuery.ProductId));
    }

    [Fact]
    public async Task ValidateOptions_WithADate_Passes()
    {
        var result = await _optionsValidator.ValidateAsync(new TicketPrintOptionsQuery
        {
            ProductId = Guid.NewGuid(),
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateLatest_WithAnEmptyProductId_Fails()
    {
        var result = await _latestValidator.ValidateAsync(new TicketPrintLatestQuery { ProductId = Guid.Empty });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateLatest_WithARealProductId_Passes()
    {
        var result = await _latestValidator.ValidateAsync(new TicketPrintLatestQuery { ProductId = Guid.NewGuid() });

        result.IsValid.Should().BeTrue();
    }
}
