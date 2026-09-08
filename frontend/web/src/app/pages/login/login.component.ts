import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/services/auth.service';
import { safeReturnUrl } from '../../core/utils/return-url.util';

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
  private readonly route = inject(ActivatedRoute);

  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly hidePassword = signal(true);

  private readonly queryParamMap = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });

  /** `{ returnUrl }` when this page was reached via an interrupted purchase, `{}` otherwise — bound
   * onto the "Registrujte se" link so switching to registration doesn't drop it. Not re-validated
   * through `safeReturnUrl` here: this only ever forwards it to another one of this app's own pages
   * (register → verify-email → login), each of which validates it again before actually navigating
   * anywhere with it. */
  readonly returnUrlParams = computed(() => {
    const returnUrl = this.queryParamMap().get('returnUrl');
    return returnUrl ? { returnUrl } : {};
  });

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
      // `?returnUrl=` is where the authGuard sent an interrupted navigation (e.g. clicking "Kupi
      // ulaznicu" while signed out) — completing login here resumes it instead of always landing on
      // the home page. Validated through safeReturnUrl so a crafted query param can't redirect a
      // freshly-signed-in visitor off this site.
      next: () => this.router.navigateByUrl(safeReturnUrl(this.queryParamMap().get('returnUrl'))),
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
