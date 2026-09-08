import { DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import type { Stripe, StripeElements } from '@stripe/stripe-js';

import { CartHoldGroup, CartService, holdGroupTotal } from '../../core/services/cart.service';
import { PurchaseService } from '../../core/services/purchase.service';
import { StripeService } from '../../core/services/stripe.service';
import { ThemeService } from '../../core/services/theme.service';
import { PaymentIntentResponse, PurchaseResponse } from '../../core/models/purchase.models';
import { extractErrorMessage, stripeErrorMessage } from '../../core/utils/api-error.util';

const STEP_LABELS = ['Pregled narudžbe', 'Način plaćanja', 'Kontakt podaci', 'Plaćanje'];

/** Same `^\d{12,19}$` the backend enforces, applied after stripping spaces — the form only ever
 * sees a card number typed with visual grouping ("0000 0000 ..."). Mock provider only: with Stripe
 * the card is typed into Stripe's own iframe and never touches this form. */
function cardNumberValidator(control: { value: string | null }) {
  const digits = (control.value ?? '').replace(/\s/g, '');
  return /^\d{12,19}$/.test(digits) ? null : { cardNumber: true };
}

@Component({
  selector: 'app-checkout',
  imports: [ReactiveFormsModule, RouterLink, DecimalPipe],
  templateUrl: './checkout.component.html',
  styleUrl: './checkout.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CheckoutComponent {
  private readonly cartService = inject(CartService);
  private readonly purchaseService = inject(PurchaseService);
  private readonly stripeService = inject(StripeService);
  private readonly themeService = inject(ThemeService);
  private readonly formBuilder = inject(FormBuilder);

  readonly cart = this.cartService.state;
  readonly stepLabels = STEP_LABELS;

  readonly step = signal(0);
  readonly done = signal(false);
  readonly isSubmitting = signal(false);
  readonly isPreparing = signal(false);
  readonly submitError = signal<string | null>(null);
  readonly results = signal<PurchaseResponse[]>([]);

  /** Which hold of the cart is being paid for. Each hold is its own order and its own payment, so a
   * multi-sector basket is paid one hold at a time — with manual capture each authorization is
   * independently cancellable, which a single basket-wide charge could not be. */
  readonly holdIndex = signal(0);
  readonly intent = signal<PaymentIntentResponse | null>(null);

  readonly totalHolds = computed(() => this.cart()?.holds.length ?? 0);

  /** How many holds the basket started with — `results().length + totalHolds()`, extracted
   * because it's used both here and in `buildFailureMessage`. The sum is invariant for the whole
   * checkout: `advanceToNextHold` only ever removes a hold from the cart in the same tick that
   * `completePurchase` pushes its result, and a failed purchase leaves the hold alone, so nothing
   * needs "start" semantics beyond naming the expression once. */
  readonly totalHoldsAtStart = computed(() => this.results().length + this.totalHolds());
  readonly isMockProvider = computed(() => this.intent()?.provider === 'Mock');

  private readonly paymentElementHost = viewChild<ElementRef<HTMLDivElement>>('paymentElement');

  private stripe: Stripe | null = null;
  private elements: StripeElements | null = null;

  // Email/phone are display-only (never sent in PurchaseRequest) so they're just required, not
  // format-checked. The card fields exist only for the Mock provider and are validated to mirror
  // PurchaseRequestValidator's SimulatedLast4 rule, per .claude/rules/00-workflow-and-testing.md.
  readonly detailsForm = this.formBuilder.nonNullable.group({
    email: ['', Validators.required],
    phone: ['', Validators.required],
  });

  readonly mockCardForm = this.formBuilder.nonNullable.group({
    cardNumber: ['', [Validators.required, cardNumberValidator]],
    cardExpiry: ['', [Validators.required, Validators.pattern(/^(0[1-9]|1[0-2])\/\d{2}$/)]],
    cardCvv: ['', [Validators.required, Validators.pattern(/^\d{3,4}$/)]],
  });

  readonly grandTotal = computed(() => (this.cart()?.holds ?? []).reduce((sum, h) => sum + holdGroupTotal(h), 0));

  /** What this hold alone costs — the amount actually being authorized right now, which is not the
   * basket total when the cart has more than one hold. */
  readonly currentHoldTotal = computed(() => {
    const hold = this.cart()?.holds[this.holdIndex()];
    return hold ? holdGroupTotal(hold) : 0;
  });

  readonly paymentMethodLabel = 'Kreditna/Debitna kartica';

  constructor() {
    // Mounts the card field as soon as both the intent and its host element exist. It cannot be
    // done inline in prepare(): the host only enters the DOM once the template renders step 3.
    effect(() => {
      const host = this.paymentElementHost();
      const intent = this.intent();
      if (!host || !intent || intent.provider !== 'Stripe' || !this.elements) return;

      this.elements.getElement('payment') ?? this.elements.create('payment').mount(host.nativeElement);
    });

    // Stripe renders inside an iframe and never sees our CSS, so a theme toggle has to be pushed in
    // explicitly or the card field stays light on a dark page.
    effect(() => {
      this.themeService.theme();
      if (this.elements) this.stripeService.retheme(this.elements);
    });
  }

  goBack(): void {
    this.step.update((s) => Math.max(0, s - 1));
  }

  goNext(): void {
    if (this.step() === 2 && this.detailsForm.invalid) {
      this.detailsForm.markAllAsTouched();
      return;
    }

    const next = Math.min(3, this.step() + 1);
    this.step.set(next);

    if (next === 3 && !this.intent()) {
      void this.preparePayment();
    }
  }

  /**
   * Asks the backend to price this hold and create the payment object for it. The amount comes back
   * from the server — this app never states a price — and so does the provider name, which is how it
   * knows whether to show Stripe's card field or the mock form.
   */
  private async preparePayment(): Promise<void> {
    const hold = this.cart()?.holds[this.holdIndex()];
    if (!hold) return;

    this.isPreparing.set(true);
    this.submitError.set(null);
    this.elements = null;

    try {
      const intent = await this.requestIntent(hold);
      this.intent.set(intent);

      if (intent.provider === 'Stripe' && intent.publishableKey && intent.clientSecret) {
        this.stripe = await this.stripeService.load(intent.publishableKey);
        if (this.stripe) {
          this.elements = this.stripeService.createElements(this.stripe, intent.clientSecret);
        }
      }
    } catch (error: unknown) {
      this.submitError.set(extractErrorMessage(error, 'Priprema plaćanja nije uspjela. Pokušajte ponovo.'));
    } finally {
      this.isPreparing.set(false);
    }
  }

  private requestIntent(hold: CartHoldGroup): Promise<PaymentIntentResponse> {
    return new Promise((resolve, reject) => {
      this.purchaseService
        .createPaymentIntent({
          holdId: hold.holdId,
          lineItems: hold.lineItems.map((li) => ({ ticketTypeId: li.ticketTypeId, quantity: li.quantity })),
        })
        .subscribe({ next: resolve, error: reject });
    });
  }

  /**
   * Confirms the payment with the provider, then tells the backend to capture it and mint tickets.
   *
   * The two calls are deliberately in that order and deliberately separate: the intent is authorized
   * with manual capture, so between them the buyer's money is only ring-fenced. If the Redis hold
   * lapsed while they were typing, the backend cancels the authorization and nothing is taken.
   */
  async finishPayment(): Promise<void> {
    const cart = this.cart();
    const intent = this.intent();
    // The guard is set BEFORE the first await, so a double-click cannot start two payments.
    if (!cart || !intent || this.isSubmitting()) return;

    if (this.isMockProvider() && this.mockCardForm.invalid) {
      this.mockCardForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.submitError.set(null);

    try {
      if (intent.provider === 'Stripe') {
        const confirmed = await this.confirmWithStripe(intent);
        if (!confirmed) return;
      }

      await this.completePurchase(cart.holds[this.holdIndex()], intent);
    } catch (error: unknown) {
      this.submitError.set(this.buildFailureMessage(extractErrorMessage(error, 'Plaćanje nije uspjelo. Pokušajte ponovo.')));
    } finally {
      this.isSubmitting.set(false);
    }
  }

  /** @returns true when the buyer's payment was authorized, false when it was declined (in which
   * case the inline error has already been set and the hold is deliberately left alive so they can
   * try another card). */
  private async confirmWithStripe(intent: PaymentIntentResponse): Promise<boolean> {
    if (!this.stripe || !this.elements || !intent.clientSecret) {
      this.submitError.set('Plaćanje nije spremno. Osvježite stranicu i pokušajte ponovo.');
      return false;
    }

    const submitted = await this.elements.submit();
    if (submitted.error) {
      this.submitError.set(stripeErrorMessage(submitted.error));
      return false;
    }

    // redirect: 'if_required' keeps 3-D Secure in Stripe's own in-page modal. The intent is created
    // with allow_redirects "never", so no payment method can navigate away and blow the five-minute
    // hold; return_url is insurance for the paths Stripe still insists on having one for.
    const result = await this.stripe.confirmPayment({
      elements: this.elements,
      clientSecret: intent.clientSecret,
      confirmParams: { return_url: `${window.location.origin}/placanje` },
      redirect: 'if_required',
    });

    if (result.error) {
      this.submitError.set(stripeErrorMessage(result.error));
      return false;
    }

    return true;
  }

  private completePurchase(hold: CartHoldGroup, intent: PaymentIntentResponse): Promise<void> {
    return new Promise((resolve, reject) => {
      this.purchaseService
        .purchase({
          holdId: hold.holdId,
          lineItems: hold.lineItems.map((li) => ({ ticketTypeId: li.ticketTypeId, quantity: li.quantity })),
          orderId: intent.orderId,
          paymentIntentId: intent.intentId,
          // Only the Mock provider reads this; with Stripe the card never reaches this form.
          simulatedLast4: this.isMockProvider()
            ? this.mockCardForm.getRawValue().cardNumber.replace(/\s/g, '').slice(-4)
            : null,
        })
        .subscribe({
          next: (response) => {
            this.results.update((acc) => [...acc, response]);
            this.advanceToNextHold();
            resolve();
          },
          error: reject,
        });
    });
  }

  /**
   * Drops the just-paid hold from the cart and either moves on to the next one or finishes.
   * Trimming as we go means a failure partway through a multi-sector basket leaves exactly the
   * unpaid holds behind, so retrying cannot double-charge for one already bought.
   */
  private advanceToNextHold(): void {
    const cart = this.cart();
    if (!cart) return;

    const remaining = cart.holds.filter((_, i) => i !== this.holdIndex());

    if (remaining.length === 0) {
      this.cartService.clear();
      this.done.set(true);
      return;
    }

    this.cartService.set({ ...cart, holds: remaining });
    this.intent.set(null);
    this.elements = null;
    this.mockCardForm.reset();
    void this.preparePayment();
  }

  private buildFailureMessage(base: string): string {
    const succeeded = this.results().length;
    if (succeeded === 0) return base;

    return `${base} (${succeeded} od ${this.totalHoldsAtStart()} narudžbi je uspješno obrađeno. Preostale možete pokušati ponovo.)`;
  }
}
