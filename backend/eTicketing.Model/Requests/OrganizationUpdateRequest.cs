namespace eTicketing.Model.Requests;

public class OrganizationUpdateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; }
}
