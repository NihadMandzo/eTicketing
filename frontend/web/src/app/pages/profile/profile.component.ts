import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthService } from '../../core/services/auth.service';
import { PurchaseService } from '../../core/services/purchase.service';
import { Ticket } from '../../core/models/purchase.models';

type TicketFilter = 'sve' | 'SingleOccurrence' | 'DailyEntry' | 'RecurringReservation';

function newPasswordsMatchValidator(control: AbstractControl): ValidationErrors | null {
  const newPassword = control.get('newPassword')?.value;
  const confirmPassword = control.get('confirmPassword')?.value;
  return newPassword === confirmPassword ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly purchaseService = inject(PurchaseService);
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
  readonly ticketFilter = signal<TicketFilter>('sve');

  // Ticket doesn't carry TicketingMode directly (denormalized only as far as
  // SectorId/ProductId) — SingleOccurrence tickets have no ValidDate/ValidFrom,
  // DailyEntry has ValidDate, RecurringReservation has ValidFrom/ValidTo. Good
  // enough to filter by without a second request per ticket.
  private modeOf(ticket: Ticket): TicketFilter {
    if (ticket.validDate) return 'DailyEntry';
    if (ticket.validFrom) return 'RecurringReservation';
    return 'SingleOccurrence';
  }

  readonly filteredTickets = computed(() => {
    const filter = this.ticketFilter();
    if (filter === 'sve') return this.tickets();
    return this.tickets().filter((t) => this.modeOf(t) === filter);
  });

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

    this.purchaseService.getMyTickets().subscribe({
      next: (result) => {
        this.tickets.set(result.items);
        this.isLoadingTickets.set(false);
      },
      error: () => {
        this.isLoadingTickets.set(false);
      },
    });
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
