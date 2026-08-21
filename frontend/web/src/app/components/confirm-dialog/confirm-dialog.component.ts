import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { AfterViewInit, ChangeDetectionStrategy, Component, ElementRef, OnDestroy, PLATFORM_ID, inject, input, output } from '@angular/core';

const FOCUSABLE_SELECTOR = 'button, a[href], input, select, textarea, [tabindex]:not([tabindex="-1"])';

/**
 * Shared confirmation dialog for every PUT/PATCH/DELETE-triggering action in this app — one
 * component, reused everywhere a mutating action needs a "are you sure?" step, rather than each
 * call site rolling its own. Structurally mirrors TicketDetailModalComponent (this app's only
 * other overlay): standalone, backdrop + role="dialog", Escape-to-close, manual Tab focus-trap,
 * focus restored on destroy, parent-owned open state via `@if` (construction/destruction IS
 * open/close, same as the ticket modal) rather than a service-based overlay.
 *
 * As of this writing `frontend/web` has no PUT/PATCH/DELETE call site yet — this app is a
 * customer storefront (browse/buy/view own tickets), and every existing mutation here is either
 * a POST (register, hold, purchase) or doesn't exist yet. This component exists ahead of that
 * need so any future mutating action (e.g. cancelling a purchase) has a ready-made, consistent
 * confirmation step to use from day one, matching the same shared-component requirement already
 * applied on desktop and mobile.
 */
@Component({
  selector: 'app-confirm-dialog',
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(document:keydown.escape)': 'cancel()',
    '(keydown.tab)': 'onTab($event)',
  },
})
export class ConfirmDialogComponent implements AfterViewInit, OnDestroy {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly previouslyFocused = this.isBrowser ? (this.document.activeElement as HTMLElement | null) : null;

  readonly title = input.required<string>();
  readonly message = input.required<string>();
  readonly confirmLabel = input('Potvrdi');
  readonly cancelLabel = input('Odustani');
  /** Set true for a destructive action (delete) to style the confirm button as a warning rather
   * than the default primary action. */
  readonly destructive = input(false);

  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  confirm(): void {
    this.confirmed.emit();
  }

  cancel(): void {
    this.cancelled.emit();
  }

  ngAfterViewInit(): void {
    if (!this.isBrowser) return;
    this.host.nativeElement.querySelector<HTMLElement>('.modal')?.focus();
  }

  ngOnDestroy(): void {
    this.previouslyFocused?.focus();
  }

  /** Keeps Tab/Shift+Tab cycling within the dialog instead of escaping into the page behind it —
   * a plain wrap-around trap since this dialog's control set (cancel + confirm) never changes
   * while open. */
  onTab(event: Event): void {
    const keyboardEvent = event as KeyboardEvent;
    const focusable = Array.from(this.host.nativeElement.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR));
    if (focusable.length === 0) return;

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = this.document.activeElement;

    if (keyboardEvent.shiftKey && active === first) {
      keyboardEvent.preventDefault();
      last.focus();
    } else if (!keyboardEvent.shiftKey && active === last) {
      keyboardEvent.preventDefault();
      first.focus();
    }
  }
}
