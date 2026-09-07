import { ApiError, ValidationProblem } from '../models/auth.models';

/**
 * Pulls a human-readable Bosnian message out of whichever error shape the API returned.
 *
 * The backend has two live error contracts and both reach this app: `{ code, message }` from the
 * Result/Error pattern, and ASP.NET Core's ValidationProblem (`{ title, status, errors }`) from
 * FluentValidation. This handles both, so components stop each keeping their own copy.
 */
export function extractErrorMessage(error: unknown, fallback = 'Došlo je do greške. Pokušajte ponovo.'): string {
  const body = (error as { error?: Partial<ApiError> & Partial<ValidationProblem> })?.error;

  if (body?.errors) {
    return Object.values(body.errors).flat().join(' ');
  }

  if (body?.code) {
    const mapped = PAYMENT_ERROR_MESSAGES[body.code];
    if (mapped) return mapped;
  }

  return body?.message ?? fallback;
}

export function errorCode(error: unknown): string | null {
  return (error as { error?: { code?: string } })?.error?.code ?? null;
}

export function errorStatus(error: unknown): number | null {
  return (error as { status?: number })?.status ?? null;
}

/**
 * Backend `Error.Code`s worth phrasing better than the generic server message.
 *
 * Anything not listed falls through to the API's own Bosnian message, which is already written for
 * buyers — this map exists for the few cases where the client can say something more actionable.
 */
const PAYMENT_ERROR_MESSAGES: Record<string, string> = {
  'purchase.hold_expired': 'Rezervacija je istekla. Vaša kartica nije terećena — odaberite ulaznice ponovo.',
  'purchase.hold_expired_refunded':
    'Rezervacija je istekla. Uplata je vraćena na vašu karticu u roku od 5-10 radnih dana.',
  'payment.unavailable': 'Plaćanje trenutno nije dostupno. Pokušajte ponovo za nekoliko minuta.',
  'payment.provider_unavailable': 'Plaćanje trenutno nije dostupno. Pokušajte ponovo za nekoliko minuta.',
};

/**
 * Stripe's client-side errors, keyed on `decline_code` first and `code` second — the same order
 * Stripe itself recommends, since `decline_code` is the specific reason and `code` the general one.
 */
const STRIPE_ERROR_MESSAGES: Record<string, string> = {
  card_declined: 'Plaćanje je odbijeno. Provjerite podatke kartice.',
  generic_decline: 'Plaćanje je odbijeno. Provjerite podatke kartice.',
  insufficient_funds: 'Na kartici nema dovoljno sredstava.',
  expired_card: 'Kartica je istekla.',
  incorrect_cvc: 'CVV nije ispravan.',
  invalid_cvc: 'CVV nije ispravan.',
  incorrect_number: 'Broj kartice nije ispravan.',
  invalid_number: 'Broj kartice nije ispravan.',
  invalid_expiry_month: 'Mjesec isteka nije ispravan.',
  invalid_expiry_year: 'Godina isteka nije ispravna.',
  authentication_required: 'Banka traži dodatnu potvrdu. Pokušajte ponovo.',
  processing_error: 'Došlo je do greške pri obradi. Pokušajte ponovo.',
};

export function stripeErrorMessage(error: { code?: string; decline_code?: string; message?: string }): string {
  return (
    STRIPE_ERROR_MESSAGES[error.decline_code ?? ''] ??
    STRIPE_ERROR_MESSAGES[error.code ?? ''] ??
    error.message ??
    'Plaćanje nije uspjelo. Pokušajte ponovo.'
  );
}
