namespace eTicketing.Notifications.Options;

/// <summary>Base URL of the Angular web app — used to build links embedded in emails (password
/// reset, "log in to your new organization"). Mobile has no equivalent native deep link (see
/// PasswordResetTemplate's doc comment), so every emailed link points here.</summary>
public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string WebBaseUrl { get; set; } = "http://localhost:4200";
}
