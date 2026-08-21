import { DOCUMENT, DatePipe, isPlatformBrowser } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  PLATFORM_ID,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';

import { Ticket } from '../../core/models/purchase.models';

const FOCUSABLE_SELECTOR = 'button, a[href], input, select, textarea, [tabindex]:not([tabindex="-1"])';

/**
 * Full-screen ticket detail overlay, opened by clicking a ticket card in
 * Profile's "Moje ulaznice" tab — the web equivalent of mobile's
 * `ticket_qr_screen.dart` (kept for feature parity between the two apps).
 * The QR is a static placeholder glyph, not a real scannable code and not a
 * fake-but-scannable-looking pattern — Ticketing has no gate-scanning
 * consumer yet, so an honest placeholder is less misleading than something
 * that visually implies real scannability. Same reasoning as mobile's.
 *
 * Rendered/destroyed by the parent's `@if`, so component construction/destruction IS modal
 * open/close — that's why focus management lives in AfterViewInit/OnDestroy rather than
 * open()/close() methods of its own.
 */
@Component({
  selector: 'app-ticket-detail-modal',
  imports: [DatePipe],
  templateUrl: './ticket-detail-modal.component.html',
  styleUrl: './ticket-detail-modal.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(document:keydown.escape)': 'close()',
    '(keydown.tab)': 'onTab($event)',
  },
})
export class TicketDetailModalComponent implements AfterViewInit, OnDestroy {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly previouslyFocused = this.isBrowser ? (this.document.activeElement as HTMLElement | null) : null;

  readonly ticket = input.required<Ticket>();
  readonly productName = input.required<string>();
  readonly closed = output<void>();

  readonly walletMessageVisible = signal(false);

  readonly validityLine = computed(() => {
    const t = this.ticket();
    if (t.validDate) return t.validDate;
    if (t.validFrom && t.validTo) return `${t.validFrom} – ${t.validTo}`;
    return null;
  });

  statusLabel(status: Ticket['status']): string {
    switch (status) {
      case 'Confirmed':
        return 'Potvrđena';
      case 'Processing':
        return 'U obradi';
      case 'Ready':
        return 'Spremna';
      case 'Cancelled':
        return 'Otkazana';
      default:
        return status;
    }
  }

  addToWallet(): void {
    this.walletMessageVisible.set(true);
  }

  close(): void {
    this.closed.emit();
  }

  ngAfterViewInit(): void {
    if (!this.isBrowser) return;
    // Focus the panel itself (tabindex="-1" in the template) rather than the first control inside
    // it — there's no single "obvious" first field here (close button vs. wallet button), so the
    // dialog's own heading is the least surprising place for a screen reader to land.
    this.host.nativeElement.querySelector<HTMLElement>('.modal')?.focus();
  }

  ngOnDestroy(): void {
    this.previouslyFocused?.focus();
  }

  /** Keeps Tab/Shift+Tab cycling within the modal instead of escaping into the page behind it —
   * a plain wrap-around trap since this dialog's control set never changes while open. */
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
