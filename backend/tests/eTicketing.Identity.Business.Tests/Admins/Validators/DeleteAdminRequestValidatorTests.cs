using eTicketing.Identity.Business.Admins;
using eTicketing.Identity.Business.Admins.Validators;
using FluentValidation.TestHelper;

namespace eTicketing.Identity.Business.Tests.Admins.Validators;

public class DeleteAdminRequestValidatorTests
{
    private readonly DeleteAdminRequestValidator _validator = new();

    [Fact]
    public void EmptyRequest_DoesNotFail()
    {
        // Reason/RecipientEmail are conditionally required (only for an OrganizationAdmin
        // target) — a role-independent business rule the validator can't see, enforced in
        // AdminService.DeleteAsync instead. The validator only checks shape.
        _validator.TestValidate(new DeleteAdminRequest()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Reason_TooLong_Fails()
    {
        var request = new DeleteAdminRequest { Reason = new string('a', 501) };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void RecipientEmail_NotAValidEmail_Fails()
    {
        var request = new DeleteAdminRequest { RecipientEmail = "not-an-email" };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.RecipientEmail);
    }

    [Fact]
    public void RecipientEmail_ValidEmail_DoesNotFail()
    {
        var request = new DeleteAdminRequest { RecipientEmail = "kontakt@acme.example" };

        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.RecipientEmail);
    }
}
