import { inject } from '@angular/core';
import { CanActivateFn, Router, RouterStateSnapshot } from '@angular/router';

import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (_route, state: RouterStateSnapshot) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  // Carries where they were going, so signing in returns them there instead of the landing page.
  // `state.url` is the full attempted URL including its own query string, which is why it is passed
  // as a single encoded value rather than merged into these params.
  return router.createUrlTree(['/prijava'], { queryParams: { returnUrl: state.url } });
};
