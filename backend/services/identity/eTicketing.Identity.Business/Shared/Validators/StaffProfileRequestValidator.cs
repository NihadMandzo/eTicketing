using FluentValidation;

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

/// <summary>Shared profile-edit rules for every "update an existing staff/organization user"
/// request — reused by <c>UpdateStaffUserRequestValidator</c> (Admins) and
/// <c>UpdateOrganizationUserRequestValidator</c> (Organizations), which previously duplicated
/// this rule set byte-for-byte. Generic on purpose, same reasoning as
/// <c>BaseSearchObjectValidator&lt;T&gt;</c>: FluentValidation's rules are tied to the concrete
/// root type, so a validator built directly for <see cref="IStaffProfileRequest"/> couldn't be
/// reused as-is by validators for the two unrelated concrete DTOs — instantiating this generic
/// per DTO type keeps the rules in one place while satisfying that requirement.</summary>
public class StaffProfileRequestValidator<T> : AbstractValidator<T> where T : IStaffProfileRequest
{
    public StaffProfileRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().Length(2, 100);
        RuleFor(x => x.LastName).NotEmpty().Length(2, 100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Username).NotEmpty().Length(3, 50);
        RuleFor(x => x.PhoneNumber).Matches(@"^\+?[0-9\s\-()]{6,20}$")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Broj telefona nije u ispravnom formatu.");
    }
}
