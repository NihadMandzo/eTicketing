using System.ComponentModel.DataAnnotations;
using eTicketing.Contracts.Pagination;

namespace eTicketing.Identity.Business.Admins;

public class CreateAdminRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }
}

public class AdminQuery : BaseSearchObject
{
}
