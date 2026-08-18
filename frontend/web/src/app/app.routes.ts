import { Routes } from '@angular/router';
import { LayoutComponent } from './components/layout/layout.component';

export const routes: Routes = [
  {
    path: '',
    component: LayoutComponent,
    children: [
      {
        path: '',
        loadChildren: () => import('./pages/home/home.routes').then(m => m.HOME_ROUTES)
      },
      {
        path: 'dogadjaji',
        loadChildren: () => import('./pages/products/products.routes').then(m => m.PRODUCTS_ROUTES)
      },
      {
        path: 'pomoc',
        loadChildren: () => import('./pages/help/help.routes').then(m => m.HELP_ROUTES)
      },
      {
        path: 'uslovi',
        loadChildren: () => import('./pages/terms/terms.routes').then(m => m.TERMS_ROUTES)
      },
      {
        path: 'privatnost',
        loadChildren: () => import('./pages/privacy/privacy.routes').then(m => m.PRIVACY_ROUTES)
      },
      {
        path: 'kontakt',
        loadChildren: () => import('./pages/contact/contact.routes').then(m => m.CONTACT_ROUTES)
      },
      {
        path: 'prijava',
        loadChildren: () => import('./pages/login/login.routes').then(m => m.LOGIN_ROUTES)
      },
      {
        path: 'registracija',
        loadChildren: () => import('./pages/register/register.routes').then(m => m.REGISTER_ROUTES)
      },
      {
        path: 'potvrda-emaila',
        loadChildren: () => import('./pages/verify-email/verify-email.routes').then(m => m.VERIFY_EMAIL_ROUTES)
      },
      {
        path: 'zaboravljena-lozinka',
        loadChildren: () => import('./pages/forgot-password/forgot-password.routes').then(m => m.FORGOT_PASSWORD_ROUTES)
      },
      {
        path: 'resetovanje-lozinke',
        loadChildren: () => import('./pages/reset-password/reset-password.routes').then(m => m.RESET_PASSWORD_ROUTES)
      },
      {
        path: 'profil',
        loadChildren: () => import('./pages/profile/profile.routes').then(m => m.PROFILE_ROUTES)
      },
      // Redirects for old English paths
      { path: 'events', redirectTo: 'dogadjaji', pathMatch: 'full' },
      { path: 'help', redirectTo: 'pomoc', pathMatch: 'full' },
      { path: 'terms', redirectTo: 'uslovi', pathMatch: 'full' },
      { path: 'privacy', redirectTo: 'privatnost', pathMatch: 'full' },
      { path: 'contact', redirectTo: 'kontakt', pathMatch: 'full' }
    ]
  }
];
