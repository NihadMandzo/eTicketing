import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/services/auth.service';
import { passwordStrengthValidator } from '../../core/utils/password.validator';

function passwordsMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('password')?.value;
  const confirmPassword = control.get('confirmPassword')?.value;
  return password === confirmPassword ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly hidePassword = signal(true);

  private readonly queryParamMap = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });

  /** `{ returnUrl }` when registration was reached via an interrupted purchase, `{}` otherwise —
   * bound onto the "Prijavite se" link, and forwarded to /potvrda-emaila on success (see submit()),
   * so the chain keeps carrying it: register → verify-email → the product page the buyer started
   * from. */
  readonly returnUrlParams = computed(() => {
    const returnUrl = this.queryParamMap().get('returnUrl');
    return returnUrl ? { returnUrl } : {};
  });

  readonly form = this.fb.nonNullable.group(
    {
      firstName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
      lastName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
      email: ['', [Validators.required, Validators.email]],
      username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(50)]],
      phoneNumber: [''],
      password: ['', [Validators.required, passwordStrengthValidator()]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatchValidator },
  );

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

    const { confirmPassword, phoneNumber, ...rest } = this.form.getRawValue();
    void confirmPassword;

    this.authService
      .register({ ...rest, phoneNumber: phoneNumber || null })
      .subscribe({
        // Registering signs the buyer in, but email verification is still required next — carrying
        // returnUrl along means that page can send them on to whatever they were trying to buy
        // instead of always landing on the home page.
        next: () => this.router.navigate(['/potvrda-emaila'], { queryParams: this.returnUrlParams() }),
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
    return httpError?.error?.message ?? 'Registracija nije uspjela. Pokušajte ponovo.';
  }
}
