using System.ComponentModel.DataAnnotations;

namespace eTicketing.Model.Requests;

public class EventTicketInsertRequest
{
    [Required(ErrorMessage = "Ticket type is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Ticket type must be between 2 and 100 characters")]
    public string TicketType { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Price is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Price must be 0 or greater")]
    public decimal Price { get; set; }
    
    [Required(ErrorMessage = "Currency is required")]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Currency must be a 3-letter ISO code (e.g., EUR, USD)")]
    public string Currency { get; set; } = "EUR";
    
    [Required(ErrorMessage = "Price type is required")]
    [RegularExpression("^(Fixed|Free|Donation|PayWhatYouWant)$", 
        ErrorMessage = "Price type must be: Fixed, Free, Donation, or PayWhatYouWant")]
    public string PriceType { get; set; } = "Fixed";
    
    [Required(ErrorMessage = "Total tickets is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Total tickets must be at least 1")]
    public int TotalTickets { get; set; }
    
    public DateTime? SaleStartDate { get; set; }
    
    public DateTime? SaleEndDate { get; set; }
    
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    [Range(1, int.MaxValue, ErrorMessage = "Minimum purchase quantity must be at least 1")]
    public int? MinPurchaseQuantity { get; set; } = 1;
    
    [Range(1, int.MaxValue, ErrorMessage = "Maximum purchase quantity must be at least 1")]
    public int? MaxPurchaseQuantity { get; set; }
    
    [Required(ErrorMessage = "Event ID is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Event ID must be valid")]
    public int EventId { get; set; }
}
