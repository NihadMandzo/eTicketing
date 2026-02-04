using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace eTicketing.Model.Requests;

public class EventUpdateRequest
{
    [Required(ErrorMessage = "Naslov je obavezan")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Naslov mora biti između 3 i 200 karaktera")]
    public string Title { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Opis je obavezan")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Opis mora biti između 10 i 2000 karaktera")]
    public string Description { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Datum i vrijeme događaja je obavezno")]
    public DateTime? EventDateTime { get; set; }
    
    [Required(ErrorMessage = "Lokacija je obavezna")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Lokacija mora biti između 3 i 500 karaktera")]
    public string Location { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Geografska širina je obavezna")]
    [Range(-90, 90, ErrorMessage = "Geografska širina mora biti između -90 i 90")]
    public double? Latitude { get; set; }
    
    [Required(ErrorMessage = "Geografska dužina je obavezna")]
    [Range(-180, 180, ErrorMessage = "Geografska dužina mora biti između -180 i 180")]
    public double? Longitude { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    [MaxLength(5, ErrorMessage = "Maksimalno 5 slika može biti dodato po ažuriranju")]
    public List<IFormFile>? NewImages { get; set; }
    
    public List<int>? ImageIdsToDelete { get; set; }
}
