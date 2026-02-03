namespace eTicketing.Model.Responses;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public bool IsFirstLogin { get; set; }
    public string? Message { get; set; }
}
