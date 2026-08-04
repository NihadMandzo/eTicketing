namespace eTicketing.Services.Database.Entities;

public class EventTicket : BaseEntity
{
    public string TicketType { get; set; } = string.Empty; // E.g., "VIP", "General", "Early Bird", "Student"
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR"; // Default currency
    public string PriceType { get; set; } = "Fixed"; // "Fixed", "Free", "Donation", "PayWhatYouWant"
    public int TotalTickets { get; set; } // Total number of tickets available
    public int TicketsSold { get; set; } = 0; // Number of tickets sold
    public int TicketsRemaining => TotalTickets - TicketsSold; // Calculated property
    public DateTime? SaleStartDate { get; set; } // When ticket sales start
    public DateTime? SaleEndDate { get; set; } // When ticket sales end (due date)
    public string? Description { get; set; } // Additional details about this ticket type
    public bool IsActive { get; set; } = true;
    public int? MinPurchaseQuantity { get; set; } = 1;
    public int? MaxPurchaseQuantity { get; set; }
    
    // Foreign Keys
    public int EventId { get; set; }
    public int OrganizationId { get; set; } // For analytics and filtering
    
    // Navigation Properties
    public Event Event { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
}
