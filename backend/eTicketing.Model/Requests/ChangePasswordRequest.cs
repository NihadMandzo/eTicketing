using System.ComponentModel.DataAnnotations;

namespace eTicketing.Model.Requests;

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Trenutna lozinka je obavezna")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nova lozinka je obavezna")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Lozinka mora imati najmanje 8 karaktera")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$", 
        ErrorMessage = "Lozinka mora sadržavati veliko slovo, malo slovo, broj i specijalni karakter")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Potvrda lozinke je obavezna")]
    [Compare("NewPassword", ErrorMessage = "Lozinke se ne podudaraju")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
