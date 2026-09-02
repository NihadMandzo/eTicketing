using eTicketing.Identity.Business.Auth;
using eTicketing.Identity.Business.Auth.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace eTicketing.Identity.Business.Tests.Shared;

/// <summary>
/// Exercises the shared <c>PasswordRules.Password()</c> through one of its callers
/// (<see cref="RegisterRequestValidator"/>) — every password-setting request in the service now
/// composes the same extension, so pinning it once here covers all eight.
///
/// The regression these guard: the rule used to be enforced only on two Flutter dialogs, whose
/// character class <c>[A-Za-z\d@$!%*?&amp;#]</c> rejected every punctuation mark it did not name.
/// A password containing a full stop, comma, hyphen, underscore or bracket was refused by the
/// form while the API would have taken it.
/// </summary>
public class PasswordRulesTests
{
    private readonly RegisterRequestValidator _validator = new();

    private static RegisterRequest Request(string password) => new()
    {
        FirstName = "Test",
        LastName = "Korisnik",
        Email = "test.korisnik@example.com",
        Username = "testkorisnik",
        Password = password,
    };

    /// <summary>Every special character the previous allow-list left out. Each of these is now a
    /// valid way to satisfy the "at least one special character" rule.</summary>
    [Theory]
    [InlineData("Lozinka123.")]
    [InlineData("Lozinka123,")]
    [InlineData("Lozinka123;")]
    [InlineData("Lozinka123'")]
    [InlineData("Lozinka123/")]
    [InlineData("Lozinka123[")]
    [InlineData("Lozinka123]")]
    [InlineData("Lozinka123\\")]
    [InlineData("Lozinka123?")]
    [InlineData("Lozinka123_")]
    [InlineData("Lozinka123-")]
    [InlineData("Lozinka123=")]
    [InlineData("Lozinka123+")]
    [InlineData("Lozinka123<")]
    [InlineData("Lozinka123>")]
    [InlineData("Lozinka123~")]
    [InlineData("Lozinka123|")]
    [InlineData("Lozinka123\"")]
    public void Password_WithAnyPunctuationAsItsSpecialCharacter_IsAccepted(string password)
    {
        _validator.TestValidate(Request(password)).ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    /// <summary>The set that already worked, kept so widening the rule didn't narrow it.</summary>
    [Theory]
    [InlineData("Lozinka123!")]
    [InlineData("Lozinka123@")]
    [InlineData("Lozinka123#")]
    [InlineData("Lozinka123$")]
    [InlineData("Lozinka123%")]
    [InlineData("Lozinka123&")]
    [InlineData("Lozinka123*")]
    public void Password_WithTheOriginallyAllowedSpecialCharacters_IsStillAccepted(string password)
    {
        _validator.TestValidate(Request(password)).ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("Lozinka1", "no special character")]
    [InlineData("lozinka123!", "no uppercase")]
    [InlineData("LOZINKA123!", "no lowercase")]
    [InlineData("LozinkaBezCifre!", "no digit")]
    [InlineData("Loz1!", "shorter than 8")]
    [InlineData("", "empty")]
    public void Password_MissingARequiredClass_IsRejected(string password, string _)
    {
        _validator.TestValidate(Request(password)).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_LongerThanTheMaximum_IsRejected()
    {
        var tooLong = "Aa1!" + new string('x', 97); // 101 characters

        _validator.TestValidate(Request(tooLong)).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_AtExactlyTheMaximum_IsAccepted()
    {
        var atLimit = "Aa1!" + new string('x', 96); // 100 characters

        _validator.TestValidate(Request(atLimit)).ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    /// <summary>A space is whitespace, not a special character — it must not satisfy the rule on
    /// its own, or "Lozinka 123" would pass while looking like it has no symbol at all.</summary>
    [Fact]
    public void Password_WhoseOnlyNonAlphanumericIsASpace_IsRejected()
    {
        _validator.TestValidate(Request("Lozinka 123")).ShouldHaveValidationErrorFor(x => x.Password);
    }

    /// <summary>Spaces are still permitted alongside a real special character — nothing here
    /// restricts which characters a password may contain.</summary>
    [Fact]
    public void Password_ContainingSpacesAlongsideASpecialCharacter_IsAccepted()
    {
        _validator.TestValidate(Request("Moja Lozinka 123!"))
            .ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    /// <summary>CascadeMode.Stop: an empty password reports only that it is required, rather than
    /// all six rules at once — the client renders these joined, so the difference is visible.</summary>
    [Fact]
    public void Password_WhenEmpty_ReportsExactlyOneError()
    {
        var result = _validator.TestValidate(Request(""));

        result.Errors.Count(e => e.PropertyName == nameof(RegisterRequest.Password)).Should().Be(1);
    }
}
