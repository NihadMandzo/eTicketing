using eTicketing.Identity.Business.Organizations;
using eTicketing.Identity.Business.Organizations.Validators;
using FluentValidation.TestHelper;

namespace eTicketing.Identity.Business.Tests.Organizations.Validators;

public class CreateOrganizationRequestValidatorTests
{
    private readonly CreateOrganizationRequestValidator _validator = new();

    private static CreateOrganizationRequest ValidRequest() => new()
    {
        Name = "Acme Events",
        Description = "Event organizer",
        Address = "Test Address 1",
        PhoneNumber = "+387 61 000 000",
        Email = "info@acme.example",
        NotificationEmail = "kontakt@acme.example",
        AdminFirstName = "Jane",
        AdminLastName = "Doe",
        AdminEmail = "jane.doe@acme.example",
        AdminUsername = "janedoe",
        AdminPassword = "SuperSecret123"
    };

    [Fact]
    public void ValidRequest_DoesNotFail()
    {
        _validator.TestValidate(ValidRequest()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NotificationEmail_Empty_Fails()
    {
        _validator.TestValidate(ValidRequest() with { NotificationEmail = "" })
            .ShouldHaveValidationErrorFor(x => x.NotificationEmail);
    }

    [Fact]
    public void NotificationEmail_NotAValidEmail_Fails()
    {
        _validator.TestValidate(ValidRequest() with { NotificationEmail = "not-an-email" })
            .ShouldHaveValidationErrorFor(x => x.NotificationEmail);
    }

    [Fact]
    public void NotificationEmail_IndependentOfAdminEmail_DoesNotFail()
    {
        // Deliberately different from AdminEmail — that's the whole point of the field (see
        // CreateOrganizationRequest's doc comment).
        var request = ValidRequest() with { NotificationEmail = "someone.else@example.com" };

        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.NotificationEmail);
    }
}
