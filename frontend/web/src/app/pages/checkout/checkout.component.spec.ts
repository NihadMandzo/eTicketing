import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';

import { CheckoutComponent } from './checkout.component';
import { CartHoldGroup, CartService, CartState } from '../../core/services/cart.service';
import { PurchaseService } from '../../core/services/purchase.service';
import { PurchaseResponse } from '../../core/models/purchase.models';

/** Records every cart written via set() while still behaving like the real signal-backed service,
 * so CheckoutComponent's `this.cart()` reads reflect whatever the SUT itself last wrote. */
class FakeCartService {
  private readonly _state = signal<CartState | null>(null);
  readonly state = this._state.asReadonly();
  readonly set = vi.fn((cart: CartState) => this._state.set(cart));
  readonly clear = vi.fn(() => this._state.set(null));

  seed(cart: CartState): void {
    this._state.set(cart);
  }
}

class FakePurchaseService {
  readonly purchase = vi.fn();
}

function buildHold(holdId: string, unitPrice: number): CartHoldGroup {
  return {
    holdId,
    sectorId: `sector-${holdId}`,
    sectorName: `Sektor ${holdId}`,
    lineItems: [{ ticketTypeId: null, ticketTypeName: null, quantity: 1, unitPrice }],
  };
}

function buildCart(holds: CartHoldGroup[]): CartState {
  return { productId: 'p1', productName: 'Test Event', date: null, holds };
}

function buildResponse(orderId: string): PurchaseResponse {
  return { orderId, productId: 'p1', sectorId: 's1', totalPaid: 50, purchasedAt: new Date().toISOString(), tickets: [] };
}

describe('CheckoutComponent', () => {
  let fixture: ComponentFixture<CheckoutComponent>;
  let component: CheckoutComponent;
  let cartService: FakeCartService;
  let purchaseService: FakePurchaseService;

  beforeEach(async () => {
    cartService = new FakeCartService();
    purchaseService = new FakePurchaseService();

    await TestBed.configureTestingModule({
      imports: [CheckoutComponent],
      providers: [
        { provide: CartService, useValue: cartService },
        { provide: PurchaseService, useValue: purchaseService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CheckoutComponent);
    component = fixture.componentInstance;

    component.detailsForm.setValue({
      email: 'buyer@example.com',
      phone: '061234567',
      cardNumber: '4111111111111111',
      cardExpiry: '12/30',
      cardCvv: '123',
    });
  });

  describe('finishPayment — sequential purchase', () => {
    it('accumulates a response per hold and clears the cart once every hold succeeds', () => {
      cartService.seed(buildCart([buildHold('hold-a', 50), buildHold('hold-b', 30)]));
      purchaseService.purchase.mockReturnValueOnce(of(buildResponse('order-a'))).mockReturnValueOnce(of(buildResponse('order-b')));

      component.finishPayment();

      expect(purchaseService.purchase).toHaveBeenCalledTimes(2);
      expect(purchaseService.purchase.mock.calls[0][0].holdId).toBe('hold-a');
      expect(purchaseService.purchase.mock.calls[1][0].holdId).toBe('hold-b');
      expect(component.results().map((r) => r.orderId)).toEqual(['order-a', 'order-b']);
      expect(component.done()).toBe(true);
      expect(component.isSubmitting()).toBe(false);
      expect(cartService.clear).toHaveBeenCalledOnce();
    });

    it('stops at the first failure but keeps the results already purchased, and trims the cart to only what is left', () => {
      cartService.seed(buildCart([buildHold('hold-a', 50), buildHold('hold-b', 30)]));
      purchaseService.purchase
        .mockReturnValueOnce(of(buildResponse('order-a')))
        .mockReturnValueOnce(throwError(() => ({ error: { message: 'Plaćanje odbijeno.' } })));

      component.finishPayment();

      expect(component.results().map((r) => r.orderId)).toEqual(['order-a']);
      expect(component.done()).toBe(false);
      expect(component.isSubmitting()).toBe(false);
      expect(component.submitError()).toContain('1 od 2');
      // hold-a already purchased, so the cart written back for a retry must only carry hold-b.
      const lastSetCall = cartService.set.mock.calls.at(-1)![0] as CartState;
      expect(lastSetCall.holds.map((h) => h.holdId)).toEqual(['hold-b']);
    });

    it('ignores a second call while a purchase is still in flight', () => {
      cartService.seed(buildCart([buildHold('hold-a', 50)]));
      const inFlight = new Subject<PurchaseResponse>();
      purchaseService.purchase.mockReturnValue(inFlight);

      component.finishPayment();
      component.finishPayment();

      expect(purchaseService.purchase).toHaveBeenCalledOnce();
    });
  });

  describe('detailsForm — mirrors PurchaseRequestValidator', () => {
    function setCard(cardNumber: string, cardExpiry: string, cardCvv: string): void {
      component.detailsForm.patchValue({ cardNumber, cardExpiry, cardCvv });
    }

    it('is valid for a well-formed card', () => {
      setCard('4111111111111111', '12/30', '123');
      expect(component.detailsForm.valid).toBe(true);
    });

    it('rejects a card number outside 12-19 digits', () => {
      setCard('123', '12/30', '123');
      expect(component.detailsForm.valid).toBe(false);
    });

    it('accepts a card number typed with spaces, same as the backend after stripping them', () => {
      setCard('4111 1111 1111 1111', '12/30', '123');
      expect(component.detailsForm.valid).toBe(true);
    });

    it('rejects an expiry not shaped MM/GG', () => {
      setCard('4111111111111111', '13/30', '123');
      expect(component.detailsForm.valid).toBe(false);
    });

    it('rejects a CVV that is not 3-4 digits', () => {
      setCard('4111111111111111', '12/30', '12');
      expect(component.detailsForm.valid).toBe(false);
    });
  });
});
