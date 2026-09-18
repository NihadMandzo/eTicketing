namespace eTicketing.Identity.Business.Shared.Validators;

/// <summary>Shape shared by every "edit an existing staff/organization user's profile" request
/// (<c>UpdateStaffUserRequest</c>, <c>UpdateOrganizationUserRequest</c>) — lets
/// <see cref="StaffProfileRequestValidator{T}"/> validate both without either feature depending
/// on the other's DTO.</summary>
public interface IStaffProfileRequest
{
    string FirstName { get; }
    string LastName { get; }
    string Email { get; }
    string Username { get; }
    string? PhoneNumber { get; }
}
