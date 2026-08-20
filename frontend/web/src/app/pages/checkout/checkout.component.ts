import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { CartHoldGroup, CartService } from '../../core/services/cart.service';
import { PurchaseService } from '../../core/services/purchase.service';
import { PurchaseResponse } from '../../core/models/purchase.models';

type PaymentMethod = 'card' | 'paypal' | 'mobile';

const STEP_LABELS = ['Pregled narudžbe', 'Način plaćanja', 'Detalji plaćanja', 'Potvrda'];

@Component({
  selector: 'app-checkout',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './checkout.component.html',
  styleUrl: './checkout.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CheckoutComponent {
  private readonly cartService = inject(CartService);
  private readonly purchaseService = inject(PurchaseService);

  readonly cart = this.cartService.state;
  readonly stepLabels = STEP_LABELS;

  readonly step = signal(0);
  readonly paymentMethod = signal<PaymentMethod>('card');
  readonly done = signal(false);
  readonly isSubmitting = signal(false);
  readonly submitError = signal<string | null>(null);
  readonly results = signal<PurchaseResponse[]>([]);

  readonly email = signal('');
  readonly phone = signal('');
  readonly cardNumber = signal('');
  readonly cardExpiry = signal('');
  readonly cardCvv = signal('');

  readonly grandTotal = computed(() => (this.cart()?.holds ?? []).reduce((sum, h) => sum + h.total, 0));

  // Same rules as PurchaseRequestValidator on the backend, mirrored here per
  // .claude/rules/00-workflow-and-testing.md — the backend still re-checks regardless.
  readonly canContinueFromDetails = computed(() => {
    if (this.paymentMethod() !== 'card') return this.email().trim().length > 0 && this.phone().trim().length > 0;
    return (
      this.email().trim().length > 0 &&
      this.phone().trim().length > 0 &&
      /^\d{12,19}$/.test(this.cardNumber().replace(/\s/g, '')) &&
      /^(0[1-9]|1[0-2])\/\d{2}$/.test(this.cardExpiry()) &&
      /^\d{3,4}$/.test(this.cardCvv())
    );
  });

  goBack(): void {
    this.step.update((s) => Math.max(0, s - 1));
  }

  goNext(): void {
    if (this.step() === 2 && !this.canContinueFromDetails()) return;
    this.step.update((s) => Math.min(3, s + 1));
  }

  selectPaymentMethod(method: PaymentMethod): void {
    this.paymentMethod.set(method);
  }

  /** Sequential, not parallel — every hold group shares the same card, so if
   * the first purchase is declined the rest would be too; stopping early
   * avoids charging attempts we already know will fail. Any hold not yet
   * purchased simply expires via its own TTL, same as an abandoned cart. */
  finishPayment(): void {
    const cart = this.cart();
    if (!cart || this.isSubmitting()) return;

    this.isSubmitting.set(true);
    this.submitError.set(null);
    this.purchaseNext(cart.holds, 0, []);
  }

  private purchaseNext(holds: CartHoldGroup[], index: number, acc: PurchaseResponse[]): void {
    if (index >= holds.length) {
      this.isSubmitting.set(false);
      this.results.set(acc);
      this.done.set(true);
      this.cartService.clear();
      return;
    }

    const hold = holds[index];
    this.purchaseService
      .purchase({
        holdId: hold.holdId,
        lineItems: hold.lineItems.map((li) => ({ ticketTypeId: li.ticketTypeId, quantity: li.quantity })),
        cardNumber: this.cardNumber().replace(/\s/g, '') || '4111111111111111',
        cardExpiry: this.cardExpiry() || '12/30',
        cardCvv: this.cardCvv() || '123',
      })
      .subscribe({
        next: (response) => this.purchaseNext(holds, index + 1, [...acc, response]),
        error: (error: unknown) => {
          this.isSubmitting.set(false);
          this.submitError.set(this.extractErrorMessage(error));
        },
      });
  }

  private extractErrorMessage(error: unknown): string {
    const httpError = error as { error?: { message?: string } };
    return httpError?.error?.message ?? 'Plaćanje nije uspjelo. Pokušajte ponovo.';
  }

  readonly paymentMethodLabel = computed(() => {
    switch (this.paymentMethod()) {
      case 'paypal':
        return 'PayPal';
      case 'mobile':
        return 'Mobilno plaćanje';
      default:
        return 'Kreditna/Debitna kartica';
    }
  });
}
