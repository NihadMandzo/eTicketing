import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';

import { AuthService } from '../../core/services/auth.service';
import { CatalogService } from '../../core/services/catalog.service';
import { PurchaseService } from '../../core/services/purchase.service';
import { SubscriptionService } from '../../core/services/subscription.service';
import { Product } from '../../core/models/catalog.models';
import { Ticket } from '../../core/models/purchase.models';
import { SUBSCRIPTION_STATUS_LABELS, Subscription } from '../../core/models/subscription.models';
import { extractErrorMessage } from '../../core/utils/api-error.util';
import {
  TICKET_VALIDITY_LABELS,
  TicketValidity,
  isUsable,
  resolveTicketValidity,
} from '../../core/utils/ticket-validity.util';
import { TicketDetailModalComponent } from '../../components/ticket-detail-modal/ticket-detail-modal.component';
import { environment } from '../../../environments/environment';
import { passwordStrengthValidator } from '../../core/utils/password.validator';

type TicketFilter = 'sve' | 'SingleOccurrence' | 'DailyEntry' | 'RecurringReservation';
type ProfileTab = 'tickets' | 'subscriptions' | 'settings';

const TICKETS_PAGE_SIZE = 20;

/** One product's worth of tickets, in the order they should be shown. Mirrors mobile's
 * `_TicketGroup` in my_tickets_screen.dart. */
interface TicketGroup {
  productId: string;
  title: string;
  subtitle: string;
  tickets: Ticket[];
}

/** The same treatment for subscriptions — "grupisati po proizvodu, i sve ostalo isto". */
interface SubscriptionGroup {
  productId: string;
  title: string;
  subtitle: string;
  subscriptions: Subscription[];
}

const MONTH_NAMES = [
  'januar', 'februar', 'mart', 'april', 'maj', 'juni',
  'juli', 'august', 'septembar', 'oktobar', 'novembar', 'decembar',
];

function formatLongDate(date: Date): string {
  return `${date.getDate()}. ${MONTH_NAMES[date.getMonth()]} ${date.getFullYear()}`;
}

/** Bosnian count agreement: 1 ulaznica, 2–4 ulaznice, 5+ ulaznica. Same `switch` as mobile's
 * `_groupSubtitle`, kept identical so the two apps read the same. */
function pluralize(count: number, one: string, few: string, many: string): string {
  if (count === 1) return `${count} ${one}`;
  if (count >= 2 && count <= 4) return `${count} ${few}`;
  return `${count} ${many}`;
}

function newPasswordsMatchValidator(control: AbstractControl): ValidationErrors | null {
  const newPassword = control.get('newPassword')?.value;
  const confirmPassword = control.get('confirmPassword')?.value;
  return newPassword === confirmPassword ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe, TicketDetailModalComponent],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly purchaseService = inject(PurchaseService);
  private readonly catalogService = inject(CatalogService);
  private readonly subscriptionService = inject(SubscriptionService);
  private readonly router = inject(Router);

  readonly currentUser = this.authService.currentUser;
  readonly initials = computed(() => {
    const user = this.currentUser();
    if (!user) return '';
    return `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase();
  });

  readonly activeTab = signal<ProfileTab>('tickets');

  readonly subscriptions = signal<Subscription[]>([]);
  readonly isLoadingSubscriptions = signal(false);
  readonly subscriptionError = signal<string | null>(null);
  readonly subscriptionMessage = signal<string | null>(null);
  readonly cancellingSubscriptionId = signal<string | null>(null);
  readonly statusLabels = SUBSCRIPTION_STATUS_LABELS;
  readonly validityLabels = TICKET_VALIDITY_LABELS;

  readonly tickets = signal<Ticket[]>([]);
  readonly isLoadingTickets = signal(true);
  readonly isLoadingMoreTickets = signal(false);
  // 0-indexed, per the locked PagedResult contract (see 01-domain.md).
  private readonly ticketsPage = signal(0);
  private readonly ticketsTotalCount = signal(0);
  readonly hasMoreTickets = computed(() => this.tickets().length < this.ticketsTotalCount());
  readonly ticketFilter = signal<TicketFilter>('sve');
  // Upcoming/past split, mirrors mobile's Nadolazeće/Iskorištene tabs in
  // my_tickets_screen.dart — kept in sync so both platforms offer the exact
  // same filtering capability over "moje ulaznice".
  readonly showUpcoming = signal(true);
  // Ticket carries no ProductName/ProductDate of its own (only
  // SectorName/ValidDate/ValidFrom/ValidTo) — SingleOccurrence tickets in
  // particular have no per-ticket date at all, since the single showing
  // date lives on Product.Date. Batch-fetched below (small N in practice)
  // for group headings, a display fallback and the upcoming/past split.
  private readonly productsById = signal<Record<string, Product>>({});

  // Ticket doesn't carry TicketingMode directly (denormalized only as far as
  // SectorId/ProductId) — SingleOccurrence tickets have no ValidDate/ValidFrom,
  // DailyEntry has ValidDate, RecurringReservation has ValidFrom/ValidTo. Good
  // enough to filter by without a second request per ticket.
  private modeOf(ticket: Ticket): TicketFilter {
    if (ticket.validDate) return 'DailyEntry';
    if (ticket.validFrom) return 'RecurringReservation';
    return 'SingleOccurrence';
  }

  /** The last date this ticket is good for — a subscription's period end, a day pass's day, or the
   * event's own date. Drives both the upcoming/past split and the validity badge. */
  private effectiveDate(ticket: Ticket): Date | null {
    const raw = ticket.validTo ?? ticket.validDate ?? this.productsById()[ticket.productId]?.date ?? null;
    return raw ? new Date(raw) : null;
  }

  /**
   * The date to *show*, which is the date of the thing the ticket admits you to and nothing else.
   * For a subscription that is the start of its period, not the end `effectiveDate` returns.
   *
   * Deliberately no `createdAt` fallback (same reasoning as mobile's `_displayDate`): `createdAt` is
   * when the ticket was *bought*, which is not a question any holder is asking — and on a
   * SingleOccurrence ticket, which carries no date of its own, it was the only date shown, so the
   * card confidently displayed the wrong day.
   */
  private displayDate(ticket: Ticket): Date | null {
    const raw = ticket.validDate ?? ticket.validFrom ?? this.productsById()[ticket.productId]?.date ?? null;
    return raw ? new Date(raw) : null;
  }

  private isUpcoming(ticket: Ticket): boolean {
    const date = this.effectiveDate(ticket);
    if (!date) return true;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return date >= today;
  }

  validityOf(ticket: Ticket): TicketValidity {
    return resolveTicketValidity(ticket, this.effectiveDate(ticket));
  }

  /** Three visual groups, not six — matching mobile's `ticketValidityColors`: usable is the
   * brand-adjacent green, spent-or-lapsed is neutral grey, and cancelled is the error red, because
   * a cancelled ticket is something gone wrong the holder may need to act on while a used or
   * expired one is simply finished. */
  validityTone(validity: TicketValidity): 'ok' | 'neutral' | 'bad' {
    if (isUsable(validity)) return 'ok';
    return validity === 'cancelled' ? 'bad' : 'neutral';
  }

  private readonly visibleTickets = computed(() => {
    const filter = this.ticketFilter();
    const upcoming = this.showUpcoming();
    return this.tickets()
      .filter((t) => filter === 'sve' || this.modeOf(t) === filter)
      .filter((t) => this.isUpcoming(t) === upcoming);
  });

  /**
   * The visible tickets, gathered under the product each belongs to.
   *
   * **Grouping is the point.** Four tickets to the same concert are one purchase and one plan for
   * one evening; as four peer rows in a flat list they read as four separate things to keep track
   * of. The product name is stated once, as a heading, and the stubs beneath it carry only what
   * differs between them (sector, ticket type, validity).
   *
   * Groups are ordered by their soonest ticket, and tickets within a group by date too, so the next
   * thing the buyer has to turn up to is always at the top. The `createdAt` fallbacks in both sorts
   * are tie-breakers only and never rendered — a dateless ticket still needs a stable position, and
   * purchase order is the sanest one to give it.
   */
  readonly ticketGroups = computed<TicketGroup[]>(() => {
    const byProduct = new Map<string, Ticket[]>();
    for (const ticket of this.visibleTickets()) {
      byProduct.set(ticket.productId, [...(byProduct.get(ticket.productId) ?? []), ticket]);
    }

    const sortKey = (ticket: Ticket) => (this.effectiveDate(ticket) ?? new Date(ticket.createdAt)).getTime();

    const groups = [...byProduct.entries()].map(([productId, tickets]): TicketGroup => {
      const sorted = [...tickets].sort((a, b) => sortKey(a) - sortKey(b));
      const product = this.productsById()[productId];
      const date = this.displayDate(sorted[0]);
      const count = pluralize(sorted.length, 'ulaznica', 'ulaznice', 'ulaznica');
      return {
        productId,
        title: product?.name ?? sorted[0].sectorName,
        subtitle: date ? `${formatLongDate(date)} · ${count}` : count,
        tickets: sorted,
      };
    });

    return groups.sort((a, b) => sortKey(a.tickets[0]) - sortKey(b.tickets[0]));
  });

  readonly subscriptionGroups = computed<SubscriptionGroup[]>(() => {
    const byProduct = new Map<string, Subscription[]>();
    for (const subscription of this.subscriptions()) {
      byProduct.set(subscription.productId, [...(byProduct.get(subscription.productId) ?? []), subscription]);
    }

    return [...byProduct.entries()]
      .map(([productId, subs]): SubscriptionGroup => {
        const sorted = [...subs].sort((a, b) => a.sectorName.localeCompare(b.sectorName, 'bs'));
        const product = this.productsById()[productId];
        return {
          productId,
          title: product?.name ?? sorted[0].sectorName,
          subtitle: pluralize(sorted.length, 'pretplata', 'pretplate', 'pretplata'),
          subscriptions: sorted,
        };
      })
      .sort((a, b) => a.title.localeCompare(b.title, 'bs'));
  });

  // Ticket detail overlay (parity with mobile's ticket_qr_screen.dart).
  readonly selectedTicket = signal<Ticket | null>(null);

  productNameFor(ticket: Ticket): string {
    return this.productsById()[ticket.productId]?.name ?? ticket.sectorName;
  }

  openTicketDetail(ticket: Ticket): void {
    this.selectedTicket.set(ticket);
  }

  closeTicketDetail(): void {
    this.selectedTicket.set(null);
  }

  readonly isSavingProfile = signal(false);
  readonly profileMessage = signal<string | null>(null);
  readonly profileError = signal<string | null>(null);

  readonly isSavingPassword = signal(false);
  readonly passwordMessage = signal<string | null>(null);
  readonly passwordError = signal<string | null>(null);

  // Mirrors eTicketing.Identity's UpdateUserRequestValidator — the backend stays authoritative and
  // re-checks regardless, but every rule it enforces has to be visible here too (see
  // .claude/rules/00-workflow-and-testing.md).
  readonly profileForm = this.fb.nonNullable.group({
    firstName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
    lastName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
    username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(50)]],
    phoneNumber: ['', [Validators.maxLength(20)]],
  });

  readonly passwordForm = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, passwordStrengthValidator()]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: newPasswordsMatchValidator },
  );

  constructor() {
    // Not logged in → nothing to show (authGuard already redirects for direct
    // navigation, but this also covers a session expiring while on the page).
    effect(() => {
      if (!this.currentUser()) {
        this.router.navigateByUrl('/prijava');
      }
    });

    effect(() => {
      const user = this.currentUser();
      if (user) {
        this.profileForm.patchValue({
          firstName: user.firstName,
          lastName: user.lastName,
          username: user.username,
          phoneNumber: user.phoneNumber ?? '',
        });
      }
    });

    this.loadTicketsPage(0);
  }

  selectTab(tab: ProfileTab): void {
    if (tab === 'subscriptions') {
      this.openSubscriptions();
      return;
    }
    this.activeTab.set(tab);
  }

  /** Appends page `page`'s tickets onto whatever's already loaded — `tickets()` is a running list
   * across every page fetched so far, not just the current page, so "Prikaži još" never has to
   * throw away what's already on screen. */
  private loadTicketsPage(page: number): void {
    const loadingSignal = page === 0 ? this.isLoadingTickets : this.isLoadingMoreTickets;
    loadingSignal.set(true);

    this.purchaseService.getMyTickets(page, TICKETS_PAGE_SIZE).subscribe({
      next: (result) => {
        this.tickets.update((existing) => (page === 0 ? result.items : [...existing, ...result.items]));
        this.ticketsPage.set(page);
        this.ticketsTotalCount.set(result.totalCount);
        loadingSignal.set(false);
        this.resolveProducts(result.items.map((t) => t.productId));
      },
      error: () => {
        loadingSignal.set(false);
      },
    });
  }

  /**
   * Fills `productsById` for any id it doesn't already hold. Both tickets and subscriptions feed
   * into it, since both are grouped under the product's name — and whichever tab loads second gets
   * its headings for free.
   *
   * Only the ids actually new to the map are requested: re-fetching known products on every
   * "Prikaži još" click would be wasted round-trips for names already on screen.
   */
  private resolveProducts(productIds: string[]): void {
    const known = this.productsById();
    const missing = [...new Set(productIds)].filter((id) => !known[id]);
    if (missing.length === 0) return;

    forkJoin(missing.map((id) => this.catalogService.getProductById(id).pipe(catchError(() => of(null))))).subscribe(
      (products) => {
        const byId = { ...this.productsById() };
        for (const product of products) {
          if (product) byId[product.id] = product;
        }
        this.productsById.set(byId);
      },
    );
  }

  loadMoreTickets(): void {
    if (this.isLoadingMoreTickets() || !this.hasMoreTickets()) return;
    this.loadTicketsPage(this.ticketsPage() + 1);
  }

  /**
   * Ticket PDFs are never stored — eTicketing.Ticketing renders the sheet per
   * request — so this is a plain API URL that works the moment the ticket
   * exists, with no "generating…" state to wait out. Same approach as
   * `ticket-detail-modal`: opened as a top-level navigation, which carries the
   * httpOnly session cookie.
   */
  pdfUrlFor(ticket: Ticket): string {
    return `${environment.apiBaseUrl}/tickets/${ticket.id}/pdf`;
  }

  /** The one line under a ticket's sector: which day it admits you, and for a subscription, the
   * period it covers. */
  ticketDateLine(ticket: Ticket): string | null {
    if (ticket.validDate) return `Datum: ${ticket.validDate}`;
    if (ticket.validFrom) return `Period: ${ticket.validFrom} – ${ticket.validTo}`;
    return null;
  }

  saveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.isSavingProfile.set(true);
    this.profileMessage.set(null);
    this.profileError.set(null);

    const { phoneNumber, ...rest } = this.profileForm.getRawValue();

    this.authService.updateUser({ ...rest, phoneNumber: phoneNumber || null }).subscribe({
      next: () => {
        this.isSavingProfile.set(false);
        this.profileMessage.set('Profil je uspješno ažuriran.');
      },
      error: (error: unknown) => {
        this.isSavingProfile.set(false);
        this.profileError.set(this.extractErrorMessage(error));
      },
    });
  }

  savePassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    this.isSavingPassword.set(true);
    this.passwordMessage.set(null);
    this.passwordError.set(null);

    this.authService.changePassword(this.passwordForm.getRawValue()).subscribe({
      next: () => {
        this.isSavingPassword.set(false);
        this.passwordMessage.set('Lozinka je uspješno promijenjena.');
        this.passwordForm.reset();
      },
      error: (error: unknown) => {
        this.isSavingPassword.set(false);
        this.passwordError.set(this.extractErrorMessage(error));
      },
    });
  }

  logout(): void {
    this.authService.logout().subscribe(() => this.router.navigateByUrl('/'));
  }

  private extractErrorMessage(error: unknown): string {
    const httpError = error as { error?: { message?: string; errors?: Record<string, string[]> } };
    const errors = httpError?.error?.errors;
    if (errors) {
      return Object.values(errors).flat().join(' ');
    }
    return httpError?.error?.message ?? 'Došlo je do greške. Pokušajte ponovo.';
  }

  /**
   * Loads the buyer's recurring reservations. Called lazily when the tab is first opened rather
   * than on init: most buyers have none, and it is a wasted request on every profile visit.
   */
  openSubscriptions(): void {
    this.activeTab.set('subscriptions');
    if (this.subscriptions().length > 0 || this.isLoadingSubscriptions()) return;

    this.loadSubscriptions();
  }

  private loadSubscriptions(): void {
    this.isLoadingSubscriptions.set(true);
    this.subscriptionError.set(null);

    this.subscriptionService.getMine().subscribe({
      next: (result) => {
        this.subscriptions.set(result.items);
        this.isLoadingSubscriptions.set(false);
        // Same product names as the ticket groups use, so both tabs head their groups identically.
        this.resolveProducts(result.items.map((s) => s.productId));
      },
      error: (error: unknown) => {
        this.isLoadingSubscriptions.set(false);
        this.subscriptionError.set(extractErrorMessage(error, 'Učitavanje pretplata nije uspjelo.'));
      },
    });
  }

  cancelSubscription(subscription: Subscription): void {
    if (this.cancellingSubscriptionId()) return;

    this.cancellingSubscriptionId.set(subscription.id);
    this.subscriptionError.set(null);
    this.subscriptionMessage.set(null);

    this.subscriptionService.cancel(subscription.id).subscribe({
      next: () => {
        this.cancellingSubscriptionId.set(null);
        // Reloaded rather than patched locally: the row stays Active on purpose (the buyer keeps the
        // period they paid for) and only CancelAtPeriodEnd flips, so the server's view is the honest
        // one to show.
        this.loadSubscriptions();
        this.subscriptionMessage.set(
          `Pretplata će biti otkazana na kraju tekućeg perioda (${subscription.currentPeriodEnd}).`,
        );
      },
      error: (error: unknown) => {
        this.cancellingSubscriptionId.set(null);
        this.subscriptionError.set(extractErrorMessage(error, 'Otkazivanje pretplate nije uspjelo.'));
      },
    });
  }
}
