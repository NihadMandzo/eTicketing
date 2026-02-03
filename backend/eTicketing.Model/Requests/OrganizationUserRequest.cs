using System.ComponentModel.DataAnnotations;

namespace eTicketing.Model.Requests;

public class OrganizationUserRequest
{
    [Required(ErrorMessage = "Ime je obavezno")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Ime mora biti između 2 i 100 karaktera")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Prezime je obavezno")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Prezime mora biti između 2 i 100 karaktera")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email je obavezan")]
    [EmailAddress(ErrorMessage = "Neispravan format email adrese")]
    [StringLength(255, ErrorMessage = "Email može imati maksimalno 255 karaktera")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Korisničko ime je obavezno")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Korisničko ime mora biti između 3 i 50 karaktera")]
    [RegularExpression(@"^[a-zA-Z0-9_.-]+$", ErrorMessage = "Korisničko ime može sadržavati samo slova, brojeve, tačku, crticu i podvlaku")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lozinka je obavezna")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Lozinka mora biti između 8 i 100 karaktera")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#])[A-Za-z\d@$!%*?&#]{8,}$", 
        ErrorMessage = "Lozinka mora sadržavati minimum jedno veliko slovo, jedno malo slovo, jedan broj i jedan specijalni karakter")]
    public string Password { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Neispravan format broja telefona")]
    [StringLength(20, ErrorMessage = "Broj telefona može imati maksimalno 20 karaktera")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Uloga je obavezna")]
    [Range(3, 4, ErrorMessage = "Uloga mora biti 3 (OrganizationSuperAdmin) ili 4 (OrganizationAdmin)")]
    public int RoleId { get; set; } // 3=OrgSuperAdmin, 4=OrgAdmin
}
