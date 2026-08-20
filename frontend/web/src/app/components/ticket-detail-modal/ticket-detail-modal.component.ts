import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';

import { Ticket } from '../../core/models/purchase.models';

/**
 * Full-screen ticket detail overlay, opened by clicking a ticket card in
 * Profile's "Moje ulaznice" tab — the web equivalent of mobile's
 * `ticket_qr_screen.dart` (kept for feature parity between the two apps).
 * The QR is a static placeholder glyph, not a real scannable code and not a
 * fake-but-scannable-looking pattern — Ticketing has no gate-scanning
 * consumer yet, so an honest placeholder is less misleading than something
 * that visually implies real scannability. Same reasoning as mobile's.
 */
@Component({
  selector: 'app-ticket-detail-modal',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './ticket-detail-modal.component.html',
  styleUrl: './ticket-detail-modal.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TicketDetailModalComponent {
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
}
