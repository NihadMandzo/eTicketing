namespace eTicketing.Services.Database.Entities;

public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Navigation Properties
    public ICollection<User> Users { get; set; } = new List<User>();
}
