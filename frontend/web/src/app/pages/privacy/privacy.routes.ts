import { Routes } from '@angular/router';

export const PRIVACY_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./privacy.component').then(m => m.PrivacyComponent)
  }
];
