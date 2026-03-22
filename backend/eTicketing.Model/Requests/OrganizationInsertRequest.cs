using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Model.Requests;

public class OrganizationInsertRequest
{
    [Required(ErrorMessage = "Naziv organizacije je obavezan")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Naziv mora biti između 2 i 200 karaktera")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Opis je obavezan")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Opis mora biti između 10 i 1000 karaktera")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Adresa je obavezna")]
    [StringLength(500, ErrorMessage = "Adresa može imati maksimalno 500 karaktera")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Broj telefona je obavezan")]
    [Phone(ErrorMessage = "Neispravan format broja telefona")]
    [StringLength(20, ErrorMessage = "Broj telefona može imati maksimalno 20 karaktera")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email je obavezan")]
    [EmailAddress(ErrorMessage = "Neispravan format email adrese")]
    [StringLength(255, ErrorMessage = "Email može imati maksimalno 255 karaktera")]
    public string Email { get; set; } = string.Empty;

    [Url(ErrorMessage = "Neispravan format web stranice")]
    [StringLength(255, ErrorMessage = "Web stranica može imati maksimalno 255 karaktera")]
    public string? Website { get; set; }

    public IFormFile? Logo { get; set; }
    
    // SuperAdmin details for the organization
    [Required(ErrorMessage = "Ime administratora je obavezno")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Ime mora biti između 2 i 100 karaktera")]
    public string AdminFirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Prezime administratora je obavezno")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Prezime mora biti između 2 i 100 karaktera")]
    public string AdminLastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email administratora je obavezan")]
    [EmailAddress(ErrorMessage = "Neispravan format email adrese")]
    [StringLength(255, ErrorMessage = "Email može imati maksimalno 255 karaktera")]
    public string AdminEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Korisničko ime administratora je obavezno")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Korisničko ime mora biti između 3 i 50 karaktera")]
    [RegularExpression(@"^[a-zA-Z0-9_.-]+$", ErrorMessage = "Korisničko ime može sadržavati samo slova, brojeve, tačku, crticu i podvlaku")]
    public string AdminUsername { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lozinka administratora je obavezna")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Lozinka mora biti između 8 i 100 karaktera")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#])[A-Za-z\d@$!%*?&#]{8,}$", 
        ErrorMessage = "Lozinka mora sadržavati minimum jedno veliko slovo, jedno malo slovo, jedan broj i jedan specijalni karakter")]
    public string AdminPassword { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Neispravan format broja telefona")]
    [StringLength(20, ErrorMessage = "Broj telefona može imati maksimalno 20 karaktera")]
    public string? AdminPhoneNumber { get; set; }
}
