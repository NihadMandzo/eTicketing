namespace eTicketing.Services.Database.Entities;

public class Event : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventDateTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Foreign Keys
    public int OrganizationId { get; set; }
    
    // Navigation Properties
    public Organization Organization { get; set; } = null!;
    public ICollection<EventImage> Images { get; set; } = new List<EventImage>();
}
