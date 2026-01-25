using System.ComponentModel.DataAnnotations;

namespace eTicketing.Model.Requests;

public class RegisterRequest
{
    [Required(ErrorMessage = "Ime je obavezno")]
    [StringLength(100, ErrorMessage = "Ime ne smije biti duže od 100 karaktera")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Prezime je obavezno")]
    [StringLength(100, ErrorMessage = "Prezime ne smije biti duže od 100 karaktera")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Korisničko ime je obavezno")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Korisničko ime mora imati između 3 i 50 karaktera")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email je obavezan")]
    [EmailAddress(ErrorMessage = "Neispravan format email adrese")]
    [StringLength(255, ErrorMessage = "Email ne smije biti duži od 255 karaktera")]
    public string Email { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "Broj telefona ne smije biti duži od 20 karaktera")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Lozinka je obavezna")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Lozinka mora imati najmanje 8 karaktera")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
    ErrorMessage = "Lozinka mora sadržavati veliko slovo, malo slovo, broj i specijalni karakter")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Potvrda lozinke je obavezna")]
    [Compare("Password", ErrorMessage = "Lozinke se ne podudaraju")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
