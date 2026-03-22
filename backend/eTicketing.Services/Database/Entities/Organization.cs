using System.ComponentModel.DataAnnotations.Schema;
namespace eTicketing.Services.Database.Entities;

public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    public int? ImageId { get; set; }
    [ForeignKey(nameof(ImageId))]
    public virtual Image? Image { get; set; }
    // Navigation Properties
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
    public ICollection<Image> Images { get; set; } = new List<Image>();
    public ICollection<EventTicket> EventTickets { get; set; } = new List<EventTicket>();
}
