namespace eTicketing.Services.Database.Entities;

public class Image : BaseEntity
{
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; } = false;
    public string EntityType { get; set; } = string.Empty; // "Event", "Organization", "Category"
    
    // Foreign Keys (nullable - only one will be set)
    public int? EventId { get; set; }
    public int? OrganizationId { get; set; }
    public int? CategoryId { get; set; }
    
    // Navigation Properties
    public Event? Event { get; set; }
    public Organization? Organization { get; set; }
    public Category? Category { get; set; }
}
