import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Mirrors the backend's shared `PasswordRules.Password()`
 * (Identity/Shared/Validators/PasswordRules.cs) — 8-100 characters with at least one lowercase,
 * one uppercase, one digit and one special character.
 *
 * "Special" is anything that is not a letter, a digit, or whitespace, defined by exclusion for the
 * same reason it is on the backend: an allow-list silently rejects every punctuation mark nobody
 * thought to list. Nothing here restricts which characters a password may contain; the rules only
 * say what it must contain at least one of.
 *
 * The form previously required nothing but a minimum length, so a password the API rejected got no
 * feedback at all until the request came back.
 */
export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MAX_LENGTH = 100;

const LOWERCASE = /[a-z]/;
const UPPERCASE = /[A-Z]/;
const DIGIT = /[0-9]/;
const SPECIAL = /[^a-zA-Z0-9\s]/;

/** The single Bosnian message shown for any strength failure, listing the whole requirement. */
export const PASSWORD_REQUIREMENT_MESSAGE =
  'Lozinka mora imati najmanje 8 karaktera i sadržavati veliko slovo, malo slovo, cifru i ' +
  "specijalni znak (npr. . , ; ' / [ ] ? _ - = + ! @ # $ % & *).";

/**
 * Reports `{ passwordStrength: true }` — plus `required`/`minlength`/`maxlength` are left to the
 * stock validators the forms already compose alongside this one, so existing templates that key
 * off those keep working.
 */
export function passwordStrengthValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value: string = control.value ?? '';

    // An empty field is `required`'s business, not this validator's — reporting both would show
    // two messages for one empty input.
    if (!value) {
      return null;
    }

    const ok =
      value.length >= PASSWORD_MIN_LENGTH &&
      value.length <= PASSWORD_MAX_LENGTH &&
      LOWERCASE.test(value) &&
      UPPERCASE.test(value) &&
      DIGIT.test(value) &&
      SPECIAL.test(value);

    return ok ? null : { passwordStrength: true };
  };
}
