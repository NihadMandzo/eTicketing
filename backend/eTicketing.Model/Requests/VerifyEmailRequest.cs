using System.ComponentModel.DataAnnotations;

namespace eTicketing.Model.Requests;

public class VerifyEmailRequest
{
    [Required(ErrorMessage = "Email je obavezan")]
    [EmailAddress(ErrorMessage = "Neispravan format email adrese")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Kod je obavezan")]
    [StringLength(10, ErrorMessage = "Neispravan kod")]
    public string Code { get; set; } = string.Empty;
}
