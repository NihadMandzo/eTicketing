using System.ComponentModel.DataAnnotations;

namespace eTicketing.Model.Requests;

public class OrganizationUpdateRequest
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

    [Url(ErrorMessage = "Neispravan format URL-a logotipa")]
    [StringLength(500, ErrorMessage = "URL logotipa može imati maksimalno 500 karaktera")]
    public string? LogoUrl { get; set; }

    public bool IsActive { get; set; }
}
