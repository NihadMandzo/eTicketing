using eTicketing.Ticketing.Data.Entities;

namespace eTicketing.Ticketing.Business.Purchases;

public record PurchaseRequest
{
    public string HoldId { get; init; } = string.Empty;
    public IReadOnlyList<PurchaseLineItemRequest> LineItems { get; init; } = [];

    // Cosmetic only — eTicketing.Payment is a deterministic mock, never real card processing. Only
    // the last 4 digits of CardNumber ever leave this service (see PurchaseService.Last4).
    public string CardNumber { get; init; } = string.Empty;
    public string CardExpiry { get; init; } = string.Empty;
    public string CardCvv { get; init; } = string.Empty;
}

public record PurchaseLineItemRequest
{
    // Null iff the held Sector has no TicketTypes (today's single-implicit-price path).
    public Guid? TicketTypeId { get; init; }
    public int Quantity { get; init; }
}

public record PurchaseResponse(
    Guid OrderId,
    Guid ProductId,
    Guid SectorId,
    decimal TotalPaid,
    DateTime PurchasedAt,
    IReadOnlyList<TicketResponse> Tickets);

public record TicketResponse(
    Guid Id,
    Guid OrderId,
    Guid SectorId,
    string SectorName,
    Guid ProductId,
    Guid? TicketTypeId,
    string? TicketTypeName,
    TicketStatus Status,
    decimal PricePaid,
    DateOnly? ValidDate,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    DateTime CreatedAt);
