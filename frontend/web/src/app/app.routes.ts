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
        path: 'dogadjaji/:id',
        loadChildren: () => import('./pages/product-details/product-details.routes').then(m => m.PRODUCT_DETAILS_ROUTES)
      },
      {
        path: 'placanje',
        loadChildren: () => import('./pages/checkout/checkout.routes').then(m => m.CHECKOUT_ROUTES)
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
      { path: 'contact', redirectTo: 'kontakt', pathMatch: 'full' },
      // Directly reachable error splashes — not fired by any guard or interceptor today (this
      // storefront has no role-gated page a signed-in buyer could be forbidden from, and a signed-
      // out visitor is correctly sent to /prijava with a returnUrl rather than shown a splash), but
      // in place and ready for whatever authorization boundary this app grows next.
      {
        path: 'nije-autorizovano',
        loadChildren: () => import('./pages/error/error.routes').then(m => m.UNAUTHORIZED_ROUTES)
      },
      {
        path: 'pristup-odbijen',
        loadChildren: () => import('./pages/error/error.routes').then(m => m.FORBIDDEN_ROUTES)
      },
      {
        path: 'greska-servera',
        loadChildren: () => import('./pages/error/error.routes').then(m => m.SERVER_ERROR_ROUTES)
      },
      // A concrete, stable path carrying the SAME 404 content as the wildcard below — see
      // app.routes.server.ts and server.ts for why this one exists at all: a bare client `**` is
      // never a discoverable page the build-time prerenderer can enumerate, so without this path
      // there is no static 404 file for the Node server to fall back to for a genuinely unmatched
      // request. Not linked anywhere; visiting it directly is harmless (it's just the 404 page).
      {
        path: 'stranica-nije-pronadjena',
        loadChildren: () => import('./pages/error/error.routes').then(m => m.NOT_FOUND_ROUTES)
      },
      // Catch-all — must stay last. Handles in-app 404s after hydration (a stale client-side link
      // clicked post-load never leaves the SPA, so this is what it actually renders); a fresh
      // request for a bad URL is answered by server.ts's explicit fallback instead, using the
      // prerendered file for the path above — see that file's comment for the mechanics.
      {
        path: '**',
        loadChildren: () => import('./pages/error/error.routes').then(m => m.NOT_FOUND_ROUTES)
      }
    ]
  }
];
