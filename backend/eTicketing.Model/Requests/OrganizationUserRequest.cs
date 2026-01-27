namespace eTicketing.Model.Requests;

public class OrganizationUserRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int RoleId { get; set; } // 3=OrgSuperAdmin, 4=OrgAdmin
}
