import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { CartHoldGroup, CartService, holdGroupTotal } from '../../core/services/cart.service';
import { PurchaseService } from '../../core/services/purchase.service';
import { PurchaseResponse } from '../../core/models/purchase.models';

const STEP_LABELS = ['Pregled narudžbe', 'Način plaćanja', 'Detalji plaćanja', 'Potvrda'];

/** Same `^\d{12,19}$` PurchaseRequestValidator enforces on the backend, applied after stripping
 * spaces — the form only ever sees a card number typed with visual grouping ("0000 0000 ..."). */
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
  private readonly formBuilder = inject(FormBuilder);

  readonly cart = this.cartService.state;
  readonly stepLabels = STEP_LABELS;

  readonly step = signal(0);
  readonly done = signal(false);
  readonly isSubmitting = signal(false);
  readonly submitError = signal<string | null>(null);
  readonly results = signal<PurchaseResponse[]>([]);

  // Field-level rules mirror PurchaseRequestValidator on the backend, per
  // .claude/rules/00-workflow-and-testing.md — the backend still re-checks regardless. Email/phone
  // are display-only (never sent in PurchaseRequest) so they're just required, not format-checked.
  readonly detailsForm = this.formBuilder.nonNullable.group({
    email: ['', Validators.required],
    phone: ['', Validators.required],
    cardNumber: ['', [Validators.required, cardNumberValidator]],
    cardExpiry: ['', [Validators.required, Validators.pattern(/^(0[1-9]|1[0-2])\/\d{2}$/)]],
    cardCvv: ['', [Validators.required, Validators.pattern(/^\d{3,4}$/)]],
  });

  readonly grandTotal = computed(() => (this.cart()?.holds ?? []).reduce((sum, h) => sum + holdGroupTotal(h), 0));

  /** Card is the only method eTicketing.Payment implements, so there is no
   * branch here any more — the step exists to show what will be charged. */
  readonly paymentMethodLabel = 'Kreditna/Debitna kartica';

  goBack(): void {
    this.step.update((s) => Math.max(0, s - 1));
  }

  goNext(): void {
    if (this.step() === 2 && this.detailsForm.invalid) {
      this.detailsForm.markAllAsTouched();
      return;
    }
    this.step.update((s) => Math.min(3, s + 1));
  }

  /** Sequential, not parallel — every hold group shares the same card, so if
   * the first purchase is declined the rest would be too; stopping early
   * avoids charging attempts we already know will fail. Each successful
   * purchase is immediately dropped from the cart (see purchaseNext), so a
   * failure partway through never re-submits an already-confirmed hold and
   * the buyer can just press "Završi plaćanje" again to pick up where the
   * run stopped. Any hold not yet purchased simply expires via its own TTL
   * if the buyer abandons the cart instead of retrying. */
  finishPayment(): void {
    const cart = this.cart();
    if (!cart || this.isSubmitting() || this.detailsForm.invalid) return;

    this.isSubmitting.set(true);
    this.submitError.set(null);
    this.purchaseNext(cart.holds, 0);
  }

  private purchaseNext(remainingHolds: CartHoldGroup[], succeededThisRun: number): void {
    if (remainingHolds.length === 0) {
      this.isSubmitting.set(false);
      this.done.set(true);
      this.cartService.clear();
      return;
    }

    const [hold, ...rest] = remainingHolds;
    const { cardNumber, cardExpiry, cardCvv } = this.detailsForm.getRawValue();

    this.purchaseService
      .purchase({
        holdId: hold.holdId,
        lineItems: hold.lineItems.map((li) => ({ ticketTypeId: li.ticketTypeId, quantity: li.quantity })),
        cardNumber: cardNumber.replace(/\s/g, ''),
        cardExpiry,
        cardCvv,
      })
      .subscribe({
        next: (response) => {
          this.results.update((acc) => [...acc, response]);

          // Drop this hold from the cart the moment it's confirmed — the whole point is that a
          // later hold in this same run failing (or the buyer retrying after such a failure) must
          // never resubmit a hold that already purchased successfully.
          const cart = this.cart();
          if (cart) this.cartService.set({ ...cart, holds: rest });

          this.purchaseNext(rest, succeededThisRun + 1);
        },
        error: (error: unknown) => {
          this.isSubmitting.set(false);
          this.submitError.set(this.buildFailureMessage(error, succeededThisRun, remainingHolds.length));
        },
      });
  }

  private buildFailureMessage(error: unknown, succeededThisRun: number, remainingCount: number): string {
    const base = this.extractErrorMessage(error);
    if (succeededThisRun === 0) return base;

    const total = succeededThisRun + remainingCount;
    return `${base} (${succeededThisRun} od ${total} narudžbi je uspješno obrađeno. Preostale možete pokušati ponovo.)`;
  }

  private extractErrorMessage(error: unknown): string {
    const httpError = error as { error?: { message?: string } };
    return httpError?.error?.message ?? 'Plaćanje nije uspjelo. Pokušajte ponovo.';
  }
}
