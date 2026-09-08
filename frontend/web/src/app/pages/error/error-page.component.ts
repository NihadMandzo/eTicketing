import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/services/auth.service';

/**
 * One component behind every "something is wrong" screen — 404, 403, 500 and the catch-all.
 *
 * The variants differ only in wording, so they are route `data` bound straight onto these inputs by
 * `withComponentInputBinding()` (see app.config.ts) rather than being four near-identical
 * components. Adding a new one is a route entry, not a file.
 *
 * The routes render the component **in place** instead of redirecting to it, so the URL the visitor
 * actually typed stays in the address bar — redirecting to `/nije-pronadjeno` would throw away the
 * one piece of evidence about what went wrong, and break the back button (Back would return to the
 * bad URL, which would redirect forward again).
 */
@Component({
  selector: 'app-error-page',
  imports: [RouterLink],
  templateUrl: './error-page.component.html',
  styleUrl: './error-page.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ErrorPageComponent {
  private readonly authService = inject(AuthService);

  /** Shown as the large glyph. A string, not a number — "404" is typography here, not arithmetic. */
  readonly code = input('404');
  readonly title = input('Stranica nije pronađena');
  readonly message = input('Adresa koju ste otvorili ne postoji, premještena je ili je link istekao.');

  /** Whether to offer a route to signing in. Only meaningful on the 403 screen, and only when the
   * visitor is not already signed in — telling someone who *is* signed in to "prijavite se" when
   * their account simply lacks the permission is a dead end that reads like a bug. */
  readonly showLogin = input(false);

  readonly offerLogin = computed(() => this.showLogin() && !this.authService.isAuthenticated());

  reload(): void {
    // Deliberately a full reload rather than a router re-navigation: this button exists on the
    // server-error screen, where the app's own in-memory state is the thing most likely to be
    // wrong.
    location.reload();
  }
}
