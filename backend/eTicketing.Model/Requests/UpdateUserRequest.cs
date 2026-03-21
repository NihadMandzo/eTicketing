using System.ComponentModel.DataAnnotations;

namespace eTicketing.Model.Requests;

public class UpdateUserRequest
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

    [StringLength(20, ErrorMessage = "Broj telefona ne smije biti duži od 20 karaktera")]
    public string? PhoneNumber { get; set; }
}
