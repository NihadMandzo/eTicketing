using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Sectors.Validators;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.Sectors.Validators;

/// <summary>
/// Exercises the validator directly (not through TicketTypeService, which never re-validates —
/// FluentValidation runs in the endpoint pipeline via ValidationFilter&lt;T&gt;) — this is the
/// authoritative place these rules are enforced.
/// </summary>
public class UpsertTicketTypeRequestValidatorTests
{
    private readonly UpsertTicketTypeRequestValidator _validator = new();

    private static UpsertTicketTypeRequest ValidRequest() => new()
    {
        Name = "Odrasli",
        Price = 10,
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
    public async Task Validate_WithTooLongName_Fails()
    {
        var request = ValidRequest() with { Name = new string('a', 101) };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithZeroPrice_Fails()
    {
        var request = ValidRequest() with { Price = 0 };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithNegativePrice_Fails()
    {
        var request = ValidRequest() with { Price = -5 };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }
}
