namespace eTicketing.Model.SearchObjects;

public class EventSearchObject : BaseSearchObject
{
    public int? OrganizationId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsActive { get; set; }
    public List<int>? CategoryIds { get; set; }
}
