import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { CheckoutComponent } from './checkout.component';
import { CartHoldGroup, CartService, CartState } from '../../core/services/cart.service';
import { PurchaseService } from '../../core/services/purchase.service';
import { StripeService } from '../../core/services/stripe.service';
import { PaymentIntentResponse, PurchaseResponse } from '../../core/models/purchase.models';

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
  readonly createPaymentIntent = vi.fn();
}

/** Stands in for Stripe.js. `load` returning null is also how the real service behaves during SSR,
 * which one test below relies on. */
class FakeStripeService {
  readonly confirmPayment = vi.fn().mockResolvedValue({});
  readonly submit = vi.fn().mockResolvedValue({});
  readonly retheme = vi.fn();

  readonly elements = {
    submit: (...args: unknown[]) => this.submit(...args),
    getElement: vi.fn().mockReturnValue({}),
    create: vi.fn().mockReturnValue({ mount: vi.fn() }),
    update: vi.fn(),
  };

  readonly load = vi.fn().mockImplementation(() =>
    Promise.resolve({ confirmPayment: (...args: unknown[]) => this.confirmPayment(...args) }),
  );

  readonly createElements = vi.fn().mockImplementation(() => this.elements);
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

function buildIntent(provider: 'Mock' | 'Stripe', orderId = 'order-1'): PaymentIntentResponse {
  return {
    provider,
    publishableKey: provider === 'Stripe' ? 'pk_test_1' : null,
    orderId,
    intentId: provider === 'Stripe' ? 'pi_1' : 'pi_mock_1',
    clientSecret: provider === 'Stripe' ? 'pi_1_secret' : 'mock',
    amount: 50,
    currency: 'eur',
    isSubscription: false,
  };
}

describe('CheckoutComponent', () => {
  let fixture: ComponentFixture<CheckoutComponent>;
  let component: CheckoutComponent;
  let cartService: FakeCartService;
  let purchaseService: FakePurchaseService;
  let stripeService: FakeStripeService;

  beforeEach(async () => {
    cartService = new FakeCartService();
    purchaseService = new FakePurchaseService();
    stripeService = new FakeStripeService();

    await TestBed.configureTestingModule({
      imports: [CheckoutComponent],
      providers: [
        { provide: CartService, useValue: cartService },
        { provide: PurchaseService, useValue: purchaseService },
        { provide: StripeService, useValue: stripeService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CheckoutComponent);
    component = fixture.componentInstance;

    component.detailsForm.setValue({ email: 'buyer@example.com', phone: '061234567' });
  });

  /** Walks the stepper to the payment step, which is what triggers the intent request. */
  async function advanceToPayment(): Promise<void> {
    component.goNext();
    component.goNext();
    component.goNext();
    await fixture.whenStable();
  }

  function fillMockCard(cardNumber = '4111111111111111', cardExpiry = '12/30', cardCvv = '123'): void {
    component.mockCardForm.setValue({ cardNumber, cardExpiry, cardCvv });
  }

  describe('payment preparation', () => {
    it('asks the backend to price the current hold and keeps the returned intent', async () => {
      cartService.seed(buildCart([buildHold('hold-a', 50)]));
      purchaseService.createPaymentIntent.mockReturnValue(of(buildIntent('Stripe')));

      await advanceToPayment();

      expect(purchaseService.createPaymentIntent).toHaveBeenCalledOnce();
      expect(purchaseService.createPaymentIntent.mock.calls[0][0].holdId).toBe('hold-a');
      expect(component.intent()?.provider).toBe('Stripe');
    });

    it('loads Stripe with the publishable key the server returned, not a compiled-in one', async () => {
      cartService.seed(buildCart([buildHold('hold-a', 50)]));
      purchaseService.createPaymentIntent.mockReturnValue(of(buildIntent('Stripe')));

      await advanceToPayment();

      expect(stripeService.load).toHaveBeenCalledWith('pk_test_1');
    });

    it('does not touch Stripe at all in Mock mode', async () => {
      cartService.seed(buildCart([buildHold('hold-a', 50)]));
      purchaseService.createPaymentIntent.mockReturnValue(of(buildIntent('Mock')));

      await advanceToPayment();

      expect(component.isMockProvider()).toBe(true);
      expect(stripeService.load).not.toHaveBeenCalled();
    });

    it('surfaces a preparation failure instead of leaving the buyer on a dead step', async () => {
      cartService.seed(buildCart([buildHold('hold-a', 50)]));
      purchaseService.createPaymentIntent.mockReturnValue(
        throwError(() => ({ error: { code: 'purchase.hold_expired', message: 'Rezervacija je istekla.' } })),
      );

      await advanceToPayment();

      expect(component.submitError()).toContain('Rezervacija je istekla');
      expect(component.intent()).toBeNull();
    });
  });

  describe('finishPayment — Stripe', () => {
    beforeEach(() => {
      cartService.seed(buildCart([buildHold('hold-a', 50)]));
      purchaseService.createPaymentIntent.mockReturnValue(of(buildIntent('Stripe')));
    });

    it('confirms with Stripe first, then asks the backend to capture and mint', async () => {
      purchaseService.purchase.mockReturnValue(of(buildResponse('order-a')));

      await advanceToPayment();
      await component.finishPayment();

      expect(stripeService.confirmPayment).toHaveBeenCalledOnce();
      expect(purchaseService.purchase).toHaveBeenCalledOnce();

      const sent = purchaseService.purchase.mock.calls[0][0];
      expect(sent.paymentIntentId).toBe('pi_1');
      expect(sent.orderId).toBe('order-1');
      // The card never reaches this app in Stripe mode, so there is nothing to simulate.
      expect(sent.simulatedLast4).toBeNull();
      expect(component.done()).toBe(true);
    });

    it('shows a Bosnian message for a declined card and never asks the backend to capture', async () => {
      stripeService.confirmPayment.mockResolvedValue({ error: { code: 'card_declined', decline_code: 'insufficient_funds' } });

      await advanceToPayment();
      await component.finishPayment();

      expect(component.submitError()).toBe('Na kartici nema dovoljno sredstava.');
      expect(purchaseService.purchase).not.toHaveBeenCalled();
      expect(component.done()).toBe(false);
    });

    it('ignores a second click while a payment is already in flight', async () => {
      let release: (value: unknown) => void = () => {};
      stripeService.confirmPayment.mockReturnValue(new Promise((resolve) => (release = resolve)));
      purchaseService.purchase.mockReturnValue(of(buildResponse('order-a')));

      await advanceToPayment();

      const first = component.finishPayment();
      const second = component.finishPayment();
      release({});
      await Promise.all([first, second]);

      expect(stripeService.confirmPayment).toHaveBeenCalledOnce();
      expect(purchaseService.purchase).toHaveBeenCalledOnce();
    });
  });

  describe('finishPayment — Mock provider', () => {
    beforeEach(() => {
      cartService.seed(buildCart([buildHold('hold-a', 50)]));
      purchaseService.createPaymentIntent.mockReturnValue(of(buildIntent('Mock')));
    });

    it('sends the last four digits so the offline gateway can decide the outcome', async () => {
      purchaseService.purchase.mockReturnValue(of(buildResponse('order-a')));

      await advanceToPayment();
      fillMockCard('4111 1111 1111 1234');
      await component.finishPayment();

      expect(purchaseService.purchase.mock.calls[0][0].simulatedLast4).toBe('1234');
      expect(stripeService.confirmPayment).not.toHaveBeenCalled();
    });

    it('refuses to submit an invalid card without calling the backend', async () => {
      await advanceToPayment();
      fillMockCard('123');
      await component.finishPayment();

      expect(purchaseService.purchase).not.toHaveBeenCalled();
      expect(component.mockCardForm.touched).toBe(true);
    });
  });

  describe('multi-hold basket', () => {
    beforeEach(() => {
      cartService.seed(buildCart([buildHold('hold-a', 50), buildHold('hold-b', 30)]));
      purchaseService.createPaymentIntent
        .mockReturnValueOnce(of(buildIntent('Stripe', 'order-a')))
        .mockReturnValueOnce(of(buildIntent('Stripe', 'order-b')));
    });

    it('pays each hold with its own intent, one after the other', async () => {
      purchaseService.purchase
        .mockReturnValueOnce(of(buildResponse('order-a')))
        .mockReturnValueOnce(of(buildResponse('order-b')));

      await advanceToPayment();
      await component.finishPayment();
      await fixture.whenStable();
      await component.finishPayment();

      // One intent per hold: with manual capture each authorization is independently cancellable,
      // which a single basket-wide charge could not be.
      expect(purchaseService.createPaymentIntent).toHaveBeenCalledTimes(2);
      expect(purchaseService.purchase.mock.calls[0][0].holdId).toBe('hold-a');
      expect(purchaseService.purchase.mock.calls[1][0].holdId).toBe('hold-b');
      expect(component.results().map((r) => r.orderId)).toEqual(['order-a', 'order-b']);
      expect(component.done()).toBe(true);
      expect(cartService.clear).toHaveBeenCalledOnce();
    });

    it('keeps what was already paid for and trims the cart to only what is left', async () => {
      purchaseService.purchase
        .mockReturnValueOnce(of(buildResponse('order-a')))
        .mockReturnValueOnce(throwError(() => ({ error: { message: 'Plaćanje odbijeno.' } })));

      await advanceToPayment();
      await component.finishPayment();
      await fixture.whenStable();
      await component.finishPayment();

      expect(component.results().map((r) => r.orderId)).toEqual(['order-a']);
      expect(component.done()).toBe(false);
      expect(component.submitError()).toContain('1 od 2');

      // hold-a is already bought, so the cart left behind for a retry must carry only hold-b —
      // otherwise retrying would charge for hold-a a second time.
      const lastSetCall = cartService.set.mock.calls.at(-1)![0] as CartState;
      expect(lastSetCall.holds.map((h) => h.holdId)).toEqual(['hold-b']);
    });
  });

  describe('mockCardForm — mirrors the backend SimulatedLast4 rule', () => {
    it('is valid for a well-formed card', () => {
      fillMockCard('4111111111111111', '12/30', '123');
      expect(component.mockCardForm.valid).toBe(true);
    });

    it('rejects a card number outside 12-19 digits', () => {
      fillMockCard('123', '12/30', '123');
      expect(component.mockCardForm.valid).toBe(false);
    });

    it('accepts a card number typed with spaces, same as the backend after stripping them', () => {
      fillMockCard('4111 1111 1111 1111', '12/30', '123');
      expect(component.mockCardForm.valid).toBe(true);
    });

    it('rejects an expiry not shaped MM/GG', () => {
      fillMockCard('4111111111111111', '13/30', '123');
      expect(component.mockCardForm.valid).toBe(false);
    });

    it('rejects a CVV that is not 3-4 digits', () => {
      fillMockCard('4111111111111111', '12/30', '12');
      expect(component.mockCardForm.valid).toBe(false);
    });
  });
});
