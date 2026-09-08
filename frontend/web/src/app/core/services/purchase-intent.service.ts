import { Injectable, signal } from '@angular/core';

/**
 * What the visitor was in the middle of buying when they were sent to sign in.
 *
 * Only one of the two selection shapes is ever populated, matching the product's TicketingMode:
 * `quantities` for SingleOccurrence/DailyEntry (keyed exactly as ProductDetailsComponent keys them,
 * `sectorId::ticketTypeId`), or `spotSectorId` for RecurringReservation.
 */
export interface PendingPurchase {
  productId: string;
  quantities: Record<string, number>;
  /** DailyEntry only — the visit date that was selected. Restored *before* the quantities, since
   * changing the date is what invalidates them. */
  date: string | null;
  /** RecurringReservation only — the parking space that was clicked. */
  spotSectorId: string | null;
}

/**
 * Carries a half-finished purchase across the sign-in detour.
 *
 * Pressing "Kupi ulaznicu" while signed out used to fail its hold with a 401, drop the visitor on
 * the login page and then, on success, dump them on the landing page — with the quantities they had
 * picked gone. They had to find the product again and re-enter everything. This is what closes that
 * loop: the selection is parked here, the login page is told where to return to via a `returnUrl`
 * query param, and ProductDetailsComponent picks the intent back up and resumes the exact action
 * that was interrupted.
 *
 * In-memory, deliberately — the same reasoning as {@link CartService}, whose doc comment this
 * mirrors. It survives client-side navigation (login is just another route in the same SPA), which
 * is the entire span it needs to cover, and it holds nothing worth persisting: quantities and a
 * product id, no auth state and no money. A hard refresh mid-detour loses it, and the visitor lands
 * on the product page with an empty form — the honest failure mode.
 */
@Injectable({ providedIn: 'root' })
export class PurchaseIntentService {
  private readonly pending = signal<PendingPurchase | null>(null);

  set(intent: PendingPurchase): void {
    this.pending.set(intent);
  }

  /**
   * Returns the intent for `productId` and clears it in the same step.
   *
   * Read-once on purpose: resuming is a one-shot action. If it were merely read, a resumed purchase
   * that the visitor then abandoned would be re-triggered by every later visit to that product page
   * for the rest of the session — the page would keep trying to buy something on their behalf.
   *
   * Returns null for a different product, so an intent left over from one product cannot fire on
   * another.
   */
  take(productId: string): PendingPurchase | null {
    const intent = this.pending();
    if (intent?.productId !== productId) return null;

    this.pending.set(null);
    return intent;
  }

  clear(): void {
    this.pending.set(null);
  }
}
