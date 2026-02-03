using System.ComponentModel.DataAnnotations;

namespace eTicketing.Model.Requests;

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Email je obavezan")]
    [EmailAddress(ErrorMessage = "Neispravan format email adrese")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kod je obavezan")]
    [StringLength(10, ErrorMessage = "Neispravan kod")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nova lozinka je obavezna")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Lozinka mora imati najmanje 8 karaktera")]
    [RegularExpression("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[\\W_]).{8,}$", ErrorMessage = "Lozinka mora sadržavati najmanje jedno malo slovo, jedno veliko slovo, jedan broj i jedan specijalan znak")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Potvrda lozinke je obavezna")]
    [Compare("NewPassword", ErrorMessage = "Lozinke se ne podudaraju")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
