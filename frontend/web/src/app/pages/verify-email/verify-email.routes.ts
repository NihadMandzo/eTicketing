import { Routes } from '@angular/router';

import { authGuard } from '../../core/guards/auth.guard';

export const VERIFY_EMAIL_ROUTES: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./verify-email.component').then((m) => m.VerifyEmailComponent),
  },
];
