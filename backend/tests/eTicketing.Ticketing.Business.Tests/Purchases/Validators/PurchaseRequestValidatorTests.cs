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
        OrderId = Guid.NewGuid(),
        PaymentIntentId = "pi_test_123",
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
    public async Task Validate_WithEmptyOrderId_Fails()
    {
        var request = ValidRequest() with { OrderId = Guid.Empty };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithEmptyPaymentIntentId_Fails()
    {
        var request = ValidRequest() with { PaymentIntentId = "" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    /// <summary>The Stripe provider never sends SimulatedLast4, so absent must be valid -- otherwise
    /// every real purchase would fail validation before reaching the service.</summary>
    [Fact]
    public async Task Validate_WithoutSimulatedLast4_Passes()
    {
        var request = ValidRequest() with { SimulatedLast4 = null };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithValidSimulatedLast4_Passes()
    {
        var request = ValidRequest() with { SimulatedLast4 = "0000" };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    /// <summary>Present but malformed is still rejected: a Mock-mode client sending nonsense should
    /// fail fast rather than reach the gateway.</summary>
    [Theory]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("abcd")]
    public async Task Validate_WithMalformedSimulatedLast4_Fails(string last4)
    {
        var request = ValidRequest() with { SimulatedLast4 = last4 };

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }
}
