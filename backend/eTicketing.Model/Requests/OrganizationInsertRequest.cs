namespace eTicketing.Model.Requests;

public class OrganizationInsertRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
    
    // SuperAdmin details for the organization
    public string AdminFirstName { get; set; } = string.Empty;
    public string AdminLastName { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminUsername { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string? AdminPhoneNumber { get; set; }
}
