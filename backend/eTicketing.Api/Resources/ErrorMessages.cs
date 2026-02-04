namespace eTicketing.Api.Resources;

/// <summary>
/// Error messages in English (default language)
/// </summary>
public static class ErrorMessages
{
    // General errors
    public const string InternalServerError = "An internal server error occurred";
    public const string InvalidRequest = "Invalid request";
    public const string InvalidArgument = "Invalid request parameters";
    public const string InvalidOperation = "Invalid operation";
    public const string ValidationFailed = "Validation failed";
    public const string ResourceNotFound = "Resource not found";
    public const string AccessDenied = "Access denied";
    public const string Unauthorized = "Unauthorized access";
    public const string RequestFailed = "Request failed";
    
    // Event errors
    public const string EventNotFound = "Event not found";
    public const string EventCreationFailed = "Failed to create event";
    public const string EventUpdateFailed = "Failed to update event";
    public const string EventDeleteSuccess = "Event successfully deleted";
    public const string EventCannotAccessNotInOrganization = "You do not have access to events - you are not assigned to an organization";
    public const string EventOnlyAdminCanCreate = "Only OrganizationSuperAdmin and OrganizationAdmin can create events";
    public const string EventCanOnlyCreateForOwnOrganization = "You can only create events for your organization";
    public const string EventOnlyAdminCanUpdate = "Only OrganizationSuperAdmin and OrganizationAdmin can update events";
    public const string EventCanOnlyUpdateOwnOrganization = "You can only update events for your organization";
    public const string EventOnlyAdminCanDelete = "Only OrganizationSuperAdmin and OrganizationAdmin can delete events";
    public const string EventCanOnlyDeleteOwnOrganization = "You can only delete events for your organization";
    
    // Organization errors
    public const string OrganizationNotFound = "Organization not found";
    public const string OrganizationNameExists = "Organization with this name already exists";
    public const string OrganizationEmailExists = "Organization with this email already exists";
    public const string OrganizationNoPermissionToCreate = "You do not have permission to create an organization";
    public const string OrganizationNoPermissionToDelete = "You do not have permission to delete this organization";
    public const string OrganizationInvalidUserRole = "Invalid role for organization user";
    public const string OrganizationCannotRemoveSelf = "You cannot remove yourself";
    public const string OrganizationCannotRemoveLastSuperAdmin = "You cannot remove the last SuperAdmin of the organization";
    public const string OrganizationNoAccessToOrganization = "You do not have access to this organization";
    
    // User errors
    public const string UserNotFound = "User not found";
    public const string UserEmailExists = "User with this email already exists";
    public const string UsernameExists = "Username is already taken";
    public const string UserNotAuthenticated = "User is not authenticated";
    public const string UserNoPermissionToRemove = "You do not have permission to remove this user";
    
    // Category errors
    public const string CategoryNameExists = "Category with this name already exists";
    
    // Authentication errors
    public const string InvalidCredentials = "Invalid credentials";
    public const string EmailNotVerified = "Please verify your email address before logging in";
    public const string InvalidVerificationCode = "Invalid verification code";
    public const string EmailVerifiedSuccess = "Email successfully verified";
    public const string InvalidResetCode = "Invalid password reset code";
    public const string PasswordResetSuccess = "Password successfully reset";
    public const string CurrentPasswordIncorrect = "Current password is incorrect";
    public const string PasswordChangedSuccess = "Password successfully changed";
    public const string PasswordResetCodeSent = "If the email exists, a reset code has been sent";
    
    // Validation errors
    public const string PasswordEmpty = "Password cannot be empty";
    public const string FileEmptyOrNull = "File is empty or null";
}

/// <summary>
/// Internal error messages for server-side logging only.
/// NEVER return these in HTTP responses or API payloads - they expose infrastructure details.
/// </summary>
internal static class InternalErrorMessages
{
    // Configuration errors - for server-side logging only
    internal const string AzureBlobStorageNotConfigured = "AzureBlobStorage connection string is not configured";
    internal const string JwtSecretNotConfigured = "JWT SecretKey is not configured";
}
