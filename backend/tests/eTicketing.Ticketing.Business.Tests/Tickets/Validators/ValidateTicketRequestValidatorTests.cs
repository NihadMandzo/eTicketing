using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Business.Tickets.Validators;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.Tickets.Validators;

public class ValidateTicketRequestValidatorTests
{
    private readonly ValidateTicketRequestValidator _sut = new();

    [Fact]
    public void Validate_ForAWellFormedRequest_Passes()
    {
        var result = _sut.Validate(Request(Guid.NewGuid(), "ETK1.abc.def"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithoutAProduct_Fails()
    {
        // Validation is always AGAINST a product — a scan with no product selected is meaningless.
        var result = _sut.Validate(Request(Guid.Empty, "ETK1.abc.def"));

        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Događaj je obavezan.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithABlankCode_Fails(string code)
    {
        var result = _sut.Validate(Request(Guid.NewGuid(), code));

        result.Errors.Should().Contain(e => e.ErrorMessage == "Kod ulaznice je obavezan.");
    }

    [Fact]
    public void Validate_AtTheMaximumCodeLength_Passes()
    {
        var result = _sut.Validate(Request(Guid.NewGuid(), new string('x', ValidateTicketRequestValidator.MaxCodeLength)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_OneCharacterOverTheMaximum_Fails()
    {
        var result = _sut.Validate(Request(Guid.NewGuid(), new string('x', ValidateTicketRequestValidator.MaxCodeLength + 1)));

        result.IsValid.Should().BeFalse();
    }

    private static ValidateTicketRequest Request(Guid productId, string code) =>
        new() { ProductId = productId, Code = code };
}
