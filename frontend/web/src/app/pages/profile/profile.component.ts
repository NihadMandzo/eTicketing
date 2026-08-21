import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';

import { AuthService } from '../../core/services/auth.service';
import { CatalogService } from '../../core/services/catalog.service';
import { PurchaseService } from '../../core/services/purchase.service';
import { Product } from '../../core/models/catalog.models';
import { Ticket } from '../../core/models/purchase.models';
import { TicketDetailModalComponent } from '../../components/ticket-detail-modal/ticket-detail-modal.component';

type TicketFilter = 'sve' | 'SingleOccurrence' | 'DailyEntry' | 'RecurringReservation';

const TICKETS_PAGE_SIZE = 20;

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
  private readonly router = inject(Router);

  readonly currentUser = this.authService.currentUser;
  readonly initials = computed(() => {
    const user = this.currentUser();
    if (!user) return '';
    return `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase();
  });

  readonly activeTab = signal<'tickets' | 'settings'>('tickets');
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
  // for both a display fallback and the upcoming/past split.
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

  private effectiveDate(ticket: Ticket): Date | null {
    const raw = ticket.validTo ?? ticket.validDate ?? this.productsById()[ticket.productId]?.date ?? null;
    return raw ? new Date(raw) : null;
  }

  private isUpcoming(ticket: Ticket): boolean {
    const date = this.effectiveDate(ticket);
    if (!date) return true;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return date >= today;
  }

  readonly filteredTickets = computed(() => {
    const filter = this.ticketFilter();
    const upcoming = this.showUpcoming();
    return this.tickets()
      .filter((t) => filter === 'sve' || this.modeOf(t) === filter)
      .filter((t) => this.isUpcoming(t) === upcoming);
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

  readonly profileForm = this.fb.nonNullable.group({
    firstName: ['', [Validators.required, Validators.minLength(2)]],
    lastName: ['', [Validators.required, Validators.minLength(2)]],
    username: ['', [Validators.required, Validators.minLength(3)]],
    phoneNumber: [''],
  });

  readonly passwordForm = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
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

        // Only fetch products this page actually introduced — productsById already carries
        // whatever earlier pages resolved, so re-fetching those every "Prikaži još" click would be
        // wasted requests for products already known.
        const alreadyKnown = this.productsById();
        const newProductIds = [...new Set(result.items.map((t) => t.productId))].filter((id) => !alreadyKnown[id]);
        if (newProductIds.length === 0) return;
        forkJoin(
          newProductIds.map((id) => this.catalogService.getProductById(id).pipe(catchError(() => of(null)))),
        ).subscribe((products) => {
          const byId = { ...this.productsById() };
          for (const product of products) {
            if (product) byId[product.id] = product;
          }
          this.productsById.set(byId);
        });
      },
      error: () => {
        loadingSignal.set(false);
      },
    });
  }

  loadMoreTickets(): void {
    if (this.isLoadingMoreTickets() || !this.hasMoreTickets()) return;
    this.loadTicketsPage(this.ticketsPage() + 1);
  }

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
}
