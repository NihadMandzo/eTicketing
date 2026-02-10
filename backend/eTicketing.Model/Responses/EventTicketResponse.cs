namespace eTicketing.Model.Responses;

public class EventTicketResponse
{
    public int Id { get; set; }
    public string TicketType { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR";
    public string PriceType { get; set; } = "Fixed";
    public int TotalTickets { get; set; }
    public int TicketsSold { get; set; }
    public int TicketsRemaining { get; set; }
    public DateTime? SaleStartDate { get; set; }
    public DateTime? SaleEndDate { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int? MinPurchaseQuantity { get; set; }
    public int? MaxPurchaseQuantity { get; set; }
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public int OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
