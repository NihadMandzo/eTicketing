namespace eTicketing.Services.Database.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    
    // Navigation Properties
    public ICollection<Image> Images { get; set; } = new List<Image>();
}
