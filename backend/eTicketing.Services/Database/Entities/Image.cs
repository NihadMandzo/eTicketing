using System.ComponentModel.DataAnnotations.Schema;
namespace eTicketing.Services.Database.Entities;

public class Image : BaseEntity
{
    public string ImageUrl { get; set; } = string.Empty;

    public virtual ICollection<EventImage> EventImages { get; set; } = new List<EventImage>();
}
