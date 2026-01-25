using System.ComponentModel.DataAnnotations;

namespace eTicketing.Model.Requests;

public class LoginRequest
{
    [Required(ErrorMessage = "Email ili korisničko ime je obavezno")]
    public string EmailOrUsername { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lozinka je obavezna")]
    public string Password { get; set; } = string.Empty;
}
