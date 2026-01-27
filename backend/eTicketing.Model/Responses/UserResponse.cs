namespace eTicketing.Model.Responses;

public class UserResponse
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsFirstLogin { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
