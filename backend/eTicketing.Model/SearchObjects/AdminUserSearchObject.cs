namespace eTicketing.Model.SearchObjects;

public class AdminUserSearchObject : BaseSearchObject
{
    public bool? IsActive { get; set; }
    public string? RoleName { get; set; }
}
