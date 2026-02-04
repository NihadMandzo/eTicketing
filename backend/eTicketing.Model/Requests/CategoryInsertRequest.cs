using System.ComponentModel.DataAnnotations;

namespace eTicketing.Model.Requests;

public class CategoryInsertRequest
{
    [Required(ErrorMessage = "Naziv kategorije je obavezan")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Naziv mora biti između 2 i 100 karaktera")]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500, ErrorMessage = "Opis može imati maksimalno 500 karaktera")]
    public string Description { get; set; } = string.Empty;
    
    [Url(ErrorMessage = "Neispravan format URL adrese")]
    [StringLength(500, ErrorMessage = "URL može imati maksimalno 500 karaktera")]
    public string IconUrl { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    [Range(0, 1000, ErrorMessage = "Redoslijed prikaza mora biti između 0 i 1000")]
    public int DisplayOrder { get; set; }
}
