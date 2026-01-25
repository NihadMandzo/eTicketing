using System.ComponentModel.DataAnnotations;

namespace eTicketing.Model.Requests;

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email je obavezan")]
    [EmailAddress(ErrorMessage = "Neispravan format email adrese")]
    public string Email { get; set; } = string.Empty;
}
