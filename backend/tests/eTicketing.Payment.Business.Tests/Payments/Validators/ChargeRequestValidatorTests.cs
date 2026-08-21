using eTicketing.Payment.Business.Payments;
using eTicketing.Payment.Business.Payments.Validators;
using FluentAssertions;

namespace eTicketing.Payment.Business.Tests.Payments.Validators;

/// <summary>
/// Exercises the validator directly — the authoritative place these rules are enforced (see
/// PaymentDtos.ChargeRequest's doc comment on CardNumberLast4 for why only 4 digits ever reach
/// this request at all; the shape itself is enforced by the Ticketing-side PurchaseRequestValidator
/// before it's ever sent here).
/// </summary>
public class ChargeRequestValidatorTests
{
    private readonly ChargeRequestValidator _validator = new();

    private static ChargeRequest ValidRequest() => new()
    {
        Amount = 100,
        OrderRef = "order-1",
        CardNumberLast4 = "1234",
    };

    [Fact]
    public async Task Validate_WithValidRequest_Passes()
    {
        var result = await _validator.ValidateAsync(ValidRequest());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithZeroAmount_Fails()
    {
        var request = ValidRequest() with { Amount = 0 };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithNegativeAmount_Fails()
    {
        var request = ValidRequest() with { Amount = -10 };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithEmptyOrderRef_Fails()
    {
        var request = ValidRequest() with { OrderRef = "" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithTooShortCardNumberLast4_Fails()
    {
        var request = ValidRequest() with { CardNumberLast4 = "123" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithTooLongCardNumberLast4_Fails()
    {
        var request = ValidRequest() with { CardNumberLast4 = "12345" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithNonDigitCardNumberLast4_Fails()
    {
        var request = ValidRequest() with { CardNumberLast4 = "abcd" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }
}
