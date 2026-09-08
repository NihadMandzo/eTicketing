import { Injectable, computed, signal } from '@angular/core';

/**
 * How many backend requests are in flight right now, and therefore whether the page should be
 * blocked.
 *
 * A **counter**, not a boolean: several requests overlap constantly on this app (a product page
 * fires the product, its sectors, the organizer and the similar-products calls at once), and a
 * boolean would be cleared by the first one to finish while three were still running. Only a
 * balanced increment/decrement can say "nothing is outstanding".
 *
 * Written exclusively by `loadingInterceptor`, which pairs every increment with a `finalize` so a
 * failed or cancelled request releases the page exactly like a successful one. Nothing else should
 * call `start()`/`stop()` — an unbalanced pair leaves the overlay stuck over a working page with no
 * way for the user to dismiss it.
 */
@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly inFlight = signal(0);

  readonly isLoading = computed(() => this.inFlight() > 0);

  /** Exposed for the overlay's accessible label — "1 zahtjev" reads differently from "6". */
  readonly pendingCount = this.inFlight.asReadonly();

  start(): void {
    this.inFlight.update((count) => count + 1);
  }

  stop(): void {
    // Clamped at zero rather than allowed to go negative: a stray unbalanced stop() would otherwise
    // put the counter into a hole that later real requests have to climb out of before the overlay
    // ever shows again.
    this.inFlight.update((count) => Math.max(0, count - 1));
  }
}
