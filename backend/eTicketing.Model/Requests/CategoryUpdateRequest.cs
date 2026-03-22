using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace eTicketing.Model.Requests;

public class CategoryUpdateRequest
{
    [Required(ErrorMessage = "Naziv kategorije je obavezan")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Naziv mora biti između 2 i 100 karaktera")]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500, ErrorMessage = "Opis može imati maksimalno 500 karaktera")]
    public string Description { get; set; } = string.Empty;
    
    // Optional: null means keep the existing icon (icon is always required on the entity)
    public IFormFile? Icon { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    [Range(0, 1000, ErrorMessage = "Redoslijed prikaza mora biti između 0 i 1000")]
    public int DisplayOrder { get; set; }
}
