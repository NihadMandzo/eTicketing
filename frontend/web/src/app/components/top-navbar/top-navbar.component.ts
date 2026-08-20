import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter } from 'rxjs/operators';

import { AuthService } from '../../core/services/auth.service';
import { ThemeService } from '../../core/services/theme.service';

/**
 * Logo on the left, one menu on the right — nothing else. The account
 * actions (profile, theme, sign out) all live behind a single trigger that
 * renders as the user's name on desktop and as a hamburger below 768px, so
 * there is exactly one menu to maintain rather than a desktop bar plus a
 * separate mobile drawer.
 */
@Component({
  selector: 'app-top-navbar',
  imports: [RouterLink],
  templateUrl: './top-navbar.component.html',
  styleUrl: './top-navbar.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(document:pointerdown)': 'onDocumentPointerDown($event)',
    '(document:keydown.escape)': 'closeMenu()',
  },
})
export class TopNavbarComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly themeService = inject(ThemeService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly currentUser = this.authService.currentUser;
  readonly isAuthenticated = this.authService.isAuthenticated;
  readonly theme = this.themeService.theme;

  readonly menuOpen = signal(false);

  /** Falls back to the email's local part so the trigger is never blank for
   * an account that registered without a first name. */
  readonly displayName = computed(() => {
    const user = this.currentUser();
    if (!user) return '';
    return user.firstName?.trim() || user.email?.split('@')[0] || 'Račun';
  });

  readonly initial = computed(() => this.displayName().charAt(0).toUpperCase());

  constructor() {
    // Routing away with the panel still open leaves it floating over the new
    // page — closing on NavigationEnd covers every link inside it at once.
    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe(() => this.closeMenu());
  }

  toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  onDocumentPointerDown(event: PointerEvent): void {
    if (this.menuOpen() && !this.host.nativeElement.contains(event.target as Node)) {
      this.closeMenu();
    }
  }

  logout(): void {
    this.closeMenu();
    this.authService.logout().subscribe(() => this.router.navigateByUrl('/'));
  }

  /** Deliberately leaves the menu open: the switch is inside it, and the
   * whole page recolours behind, so closing would hide the control the user
   * just used at the moment they want to judge the result. */
  toggleTheme(): void {
    this.themeService.toggle();
  }
}
