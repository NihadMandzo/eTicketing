import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/services/auth.service';
import { ThemeService } from '../../core/services/theme.service';

@Component({
  selector: 'app-top-navbar',
  imports: [RouterLink],
  templateUrl: './top-navbar.component.html',
  styleUrl: './top-navbar.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TopNavbarComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly themeService = inject(ThemeService);

  readonly currentUser = this.authService.currentUser;
  readonly isAuthenticated = this.authService.isAuthenticated;
  readonly theme = this.themeService.theme;

  logout(): void {
    this.authService.logout().subscribe(() => this.router.navigateByUrl('/'));
  }

  toggleTheme(): void {
    this.themeService.toggle();
  }
}
