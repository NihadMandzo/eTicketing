using System.ComponentModel.DataAnnotations.Schema;
namespace eTicketing.Services.Database.Entities;

public class EventImage : BaseEntity
{
    public int? ImageId { get; set; }
    [ForeignKey(nameof(ImageId))]
    public virtual Image? Image { get; set; }
    public bool IsPrimary { get; set; } = false;
    
    // Foreign Keys
    public int EventId { get; set; }
    
    // Navigation Properties
    public Event Event { get; set; } = null!;
}
