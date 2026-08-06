import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly hidePassword = signal(true);

  readonly form = this.fb.nonNullable.group({
    emailOrUsername: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });

  togglePasswordVisibility(): void {
    this.hidePassword.update((hidden) => !hidden);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.authService.login(this.form.getRawValue()).subscribe({
      next: () => this.router.navigateByUrl('/'),
      error: (error: unknown) => {
        this.isLoading.set(false);
        this.errorMessage.set(this.extractErrorMessage(error));
      },
    });
  }

  private extractErrorMessage(error: unknown): string {
    const httpError = error as { error?: { message?: string; errors?: Record<string, string[]> } };
    const errors = httpError?.error?.errors;
    if (errors) {
      return Object.values(errors).flat().join(' ');
    }
    return httpError?.error?.message ?? 'Prijava nije uspjela. Pokušajte ponovo.';
  }
}
