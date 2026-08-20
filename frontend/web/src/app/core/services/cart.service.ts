import { Injectable, signal } from '@angular/core';

export interface CartLineItem {
  ticketTypeId: string | null;
  ticketTypeName: string | null;
  quantity: number;
  unitPrice: number;
}

/** One Redis hold = one Sector — a buyer selecting quantities across several
 * Sectors of the same Product ends up with one hold (and, at checkout, one
 * separate POST /purchases order) per Sector, since a hold is always scoped
 * to a single Sector on the backend. */
export interface CartHoldGroup {
  holdId: string;
  sectorId: string;
  sectorName: string;
  lineItems: CartLineItem[];
  total: number;
}

export interface CartState {
  productId: string;
  productName: string;
  /** DailyEntry only — the exact calendar date every hold in this cart was placed for. */
  date: string | null;
  holds: CartHoldGroup[];
}

/**
 * Holds the pending purchase intent between a product-details page (which
 * just placed one or more holds via SectorService.hold()) and the Checkout
 * page — an in-memory signal, deliberately NOT sessionStorage: matches the
 * project's httpOnly-cookie-only security posture and sidesteps Angular
 * SSR's guard-browser-globals rule. The tradeoff (lost on a hard refresh) is
 * acceptable since this is display-only cart data, not auth state — a
 * refreshed Checkout redirects back to the product page (see
 * CheckoutComponent).
 */
@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly _state = signal<CartState | null>(null);
  readonly state = this._state.asReadonly();

  readonly grandTotal = () => (this._state()?.holds ?? []).reduce((sum, h) => sum + h.total, 0);

  set(cart: CartState): void {
    this._state.set(cart);
  }

  clear(): void {
    this._state.set(null);
  }
}
