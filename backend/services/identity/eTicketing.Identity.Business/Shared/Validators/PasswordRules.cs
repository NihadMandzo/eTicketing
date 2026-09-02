using FluentValidation;

namespace eTicketing.Identity.Business.Shared.Validators;

/// <summary>
/// The one definition of what makes an acceptable password, shared by every request that sets one
/// — registration, admin creation, organization creation, adding an organization user, and all
/// four change/reset/set-password paths.
///
/// <para>Previously each of those validators enforced length only, while two desktop dialogs
/// enforced a much stricter regex of their own. That was backwards on both counts: the frontend
/// was the only place a complexity rule existed at all (so the API accepted passwords the UI
/// refused), and its character class <c>[A-Za-z\d@$!%*?&amp;#]</c> silently *rejected* every other
/// punctuation mark — a password containing <c>.</c>, <c>,</c>, <c>-</c>, <c>_</c> or <c>[</c> was
/// turned away by the form for no reason the backend agreed with.</para>
///
/// <para><b>What counts as special:</b> any character that is not a letter, a digit, or
/// whitespace. Defining it by exclusion rather than by listing an allowed set is deliberate —
/// listing is what caused the bug, since every character nobody thought to list became forbidden.
/// Nothing here restricts which characters a password may *contain*; the rules only say what it
/// must contain at least one of.</para>
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 100;

    /// <summary>At least one character that is neither alphanumeric nor whitespace.</summary>
    public const string SpecialCharacterPattern = @"[^a-zA-Z0-9\s]";

    /// <summary>Applies the shared password rules to a string property.
    /// <c>CascadeMode.Stop</c> so an empty password reports "obavezna" alone rather than failing
    /// all six rules at once and rendering as a wall of text on the client.</summary>
    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Lozinka je obavezna.")
            .MinimumLength(MinLength).WithMessage($"Lozinka mora imati najmanje {MinLength} karaktera.")
            .MaximumLength(MaxLength).WithMessage($"Lozinka može imati najviše {MaxLength} karaktera.")
            .Matches("[a-z]").WithMessage("Lozinka mora sadržavati barem jedno malo slovo.")
            .Matches("[A-Z]").WithMessage("Lozinka mora sadržavati barem jedno veliko slovo.")
            .Matches("[0-9]").WithMessage("Lozinka mora sadržavati barem jednu cifru.")
            .Matches(SpecialCharacterPattern)
            .WithMessage(@"Lozinka mora sadržavati barem jedan specijalni znak (npr. . , ; ' / [ ] \ ? _ - = + ! @ # $ % & *).");
}
