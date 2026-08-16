using FluentValidation;

namespace eTicketing.Identity.Business.Admins.Validators;

/// <summary>Reason/RecipientEmail stay optional here — whether they're actually required
/// depends on the target user's role, which only AdminService.DeleteAsync (after loading the
/// user) can know. This validator only checks the shape of whatever was sent.</summary>
public class DeleteAdminRequestValidator : AbstractValidator<DeleteAdminRequest>
{
    public DeleteAdminRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.RecipientEmail).EmailAddress().MaximumLength(255)
            .When(x => !string.IsNullOrWhiteSpace(x.RecipientEmail));
    }
}
