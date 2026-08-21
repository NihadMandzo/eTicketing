using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Ticketing.Business.Purchases.Validators;
using FluentAssertions;

namespace eTicketing.Ticketing.Business.Tests.Purchases.Validators;

/// <summary>
/// Exercises the validator directly — field-level rules only, per this validator's own doc
/// comment; cross-entity rules (hold still valid, quantities match the hold, TicketTypeId belongs
/// to the held Sector) are covered instead by PurchaseServiceTests, since they need Redis/DB
/// lookups this validator doesn't have access to.
/// </summary>
public class PurchaseRequestValidatorTests
{
    private readonly PurchaseRequestValidator _validator = new();

    private static PurchaseRequest ValidRequest() => new()
    {
        HoldId = "hold-1",
        LineItems = [new PurchaseLineItemRequest { TicketTypeId = null, Quantity = 2 }],
        CardNumber = "4242424242424242",
        CardExpiry = "12/29",
        CardCvv = "123",
    };

    [Fact]
    public async Task Validate_WithValidRequest_Passes()
    {
        var result = await _validator.ValidateAsync(ValidRequest());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithEmptyHoldId_Fails()
    {
        var request = ValidRequest() with { HoldId = "" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithNoLineItems_Fails()
    {
        var request = ValidRequest() with { LineItems = [] };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithZeroQuantityLineItem_Fails()
    {
        var request = ValidRequest() with { LineItems = [new PurchaseLineItemRequest { TicketTypeId = null, Quantity = 0 }] };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithMixOfNullAndNonNullTicketTypeIds_Fails()
    {
        var request = ValidRequest() with
        {
            LineItems =
            [
                new PurchaseLineItemRequest { TicketTypeId = Guid.NewGuid(), Quantity = 1 },
                new PurchaseLineItemRequest { TicketTypeId = null, Quantity = 1 },
            ],
        };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithAllLineItemsCarryingTicketTypeId_Passes()
    {
        var request = ValidRequest() with
        {
            LineItems =
            [
                new PurchaseLineItemRequest { TicketTypeId = Guid.NewGuid(), Quantity = 1 },
                new PurchaseLineItemRequest { TicketTypeId = Guid.NewGuid(), Quantity = 2 },
            ],
        };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithMalformedCardNumber_Fails()
    {
        var request = ValidRequest() with { CardNumber = "not-a-card" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithMalformedCardExpiry_Fails()
    {
        var request = ValidRequest() with { CardExpiry = "13/29" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithMalformedCardCvv_Fails()
    {
        var request = ValidRequest() with { CardCvv = "12" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }
}
