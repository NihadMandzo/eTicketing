namespace eTicketing.Api.Resources;

/// <summary>
/// Error messages in Croatian/Bosnian
/// Poruke o greškama na hrvatskom/bosanskom jeziku
/// </summary>
public static class ErrorMessagesHr
{
    // General errors
    public const string InternalServerError = "Došlo je do greške na serveru";
    public const string InvalidRequest = "Neispravan zahtjev";
    public const string InvalidArgument = "Neispravni parametri zahtjeva";
    public const string InvalidOperation = "Neispravna operacija";
    public const string ValidationFailed = "Validacija nije uspjela";
    public const string ResourceNotFound = "Resurs nije pronađen";
    public const string AccessDenied = "Pristup odbijen";
    public const string Unauthorized = "Neautorizovan pristup";
    public const string RequestFailed = "Zahtjev nije uspio";
    
    // Event errors
    public const string EventNotFound = "Događaj nije pronađen";
    public const string EventCreationFailed = "Greška pri kreiranju događaja";
    public const string EventUpdateFailed = "Greška pri ažuriranju događaja";
    public const string EventDeleteSuccess = "Događaj je uspješno obrisan";
    public const string EventCannotAccessNotInOrganization = "Nemate pristup događajima - niste pridruženi organizaciji";
    public const string EventOnlyAdminCanCreate = "Samo OrganizationSuperAdmin i OrganizationAdmin mogu kreirati događaje";
    public const string EventCanOnlyCreateForOwnOrganization = "Možete kreirati događaje samo za svoju organizaciju";
    public const string EventOnlyAdminCanUpdate = "Samo OrganizationSuperAdmin i OrganizationAdmin mogu uređivati događaje";
    public const string EventCanOnlyUpdateOwnOrganization = "Možete uređivati samo događaje svoje organizacije";
    public const string EventOnlyAdminCanDelete = "Samo OrganizationSuperAdmin i OrganizationAdmin mogu brisati događaje";
    public const string EventCanOnlyDeleteOwnOrganization = "Možete brisati samo događaje svoje organizacije";
    
    // Organization errors
    public const string OrganizationNotFound = "Organizacija nije pronađena";
    public const string OrganizationNameExists = "Organizacija sa ovim imenom već postoji";
    public const string OrganizationEmailExists = "Organizacija sa ovim emailom već postoji";
    public const string OrganizationNoPermissionToCreate = "Nemate dozvolu za kreiranje organizacije";
    public const string OrganizationNoPermissionToDelete = "Nemate dozvolu za brisanje ove organizacije";
    public const string OrganizationInvalidUserRole = "Neispravna uloga za korisnika organizacije";
    public const string OrganizationCannotRemoveSelf = "Ne možete ukloniti sami sebe";
    public const string OrganizationCannotRemoveLastSuperAdmin = "Ne možete ukloniti posljednjeg SuperAdmina organizacije";
    public const string OrganizationNoAccessToOrganization = "Nemate pristup ovoj organizaciji";    
    // User errors
    public const string UserNotFound = "Korisnik nije pronađen";
    public const string UserEmailExists = "Korisnik sa ovim emailom već postoji";
    public const string UsernameExists = "Korisničko ime je već zauzeto";
    public const string UserNotAuthenticated = "Korisnik nije autentifikovan";
    public const string UserNoPermissionToRemove = "Nemate dozvolu za uklanjanje ovog korisnika";
    
    // Category errors
    public const string CategoryNameExists = "Kategorija sa ovim imenom već postoji";
    
    // Authentication errors
    public const string InvalidCredentials = "Neispravni pristupni podaci";
    public const string EmailNotVerified = "Molimo verifikujte email adresu prije prijave";
    public const string InvalidVerificationCode = "Neispravan kod za verifikaciju";
    public const string EmailVerifiedSuccess = "Email uspješno verifikovan";
    public const string InvalidResetCode = "Neispravan kod za resetovanje lozinke";
    public const string PasswordResetSuccess = "Lozinka je uspješno resetovana";
    public const string CurrentPasswordIncorrect = "Trenutna lozinka nije ispravna";
    public const string PasswordChangedSuccess = "Lozinka je uspješno promijenjena";
    public const string PasswordResetCodeSent = "Ako email postoji, kod za resetovanje je poslan";
    
    // Validation errors
    public const string PasswordEmpty = "Lozinka ne može biti prazna";
    public const string FileEmptyOrNull = "Datoteka je prazna ili ne postoji";
}

/// <summary>
/// Internal error messages for server-side logging only.
/// NEVER return these in HTTP responses or API payloads - they expose infrastructure details.
/// Interne poruke o greškama samo za server-side logovanje.
/// NIKADA ne vraćati u HTTP odgovorima ili API payloadima - otkrivaju detalje infrastrukture.
/// </summary>
internal static class InternalErrorMessagesHr
{
    // Configuration errors - for server-side logging only
    public const string AzureBlobStorageNotConfigured = "AzureBlobStorage connection string nije konfigurisan";
    public const string JwtSecretNotConfigured = "JWT SecretKey nije konfigurisan";
}
