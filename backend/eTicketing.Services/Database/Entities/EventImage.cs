namespace eTicketing.Services.Database.Entities;

public class EventImage : BaseEntity
{
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; } = false;
    
    // Foreign Keys
    public int EventId { get; set; }
    
    // Navigation Properties
    public Event Event { get; set; } = null!;
}
