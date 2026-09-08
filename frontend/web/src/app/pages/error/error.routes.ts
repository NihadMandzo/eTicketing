import { Routes } from '@angular/router';

/**
 * One `ErrorPageComponent`, four routes — each supplies its own copy through route `data`, bound
 * onto the component's inputs by `withComponentInputBinding()` (see app.config.ts). Adding a fifth
 * variant is a route entry here, not a new component.
 */
const loadErrorPage = () => import('./error-page.component').then((m) => m.ErrorPageComponent);

export const NOT_FOUND_ROUTES: Routes = [
  {
    path: '',
    loadComponent: loadErrorPage,
    data: {
      code: '404',
      title: 'Stranica nije pronađena',
      message: 'Adresa koju ste otvorili ne postoji, premještena je ili je link istekao.',
    },
  },
];

export const UNAUTHORIZED_ROUTES: Routes = [
  {
    path: '',
    loadComponent: loadErrorPage,
    data: {
      code: '401',
      title: 'Niste prijavljeni',
      message: 'Za pristup ovoj stranici potrebno je da se prijavite na svoj nalog.',
      showLogin: true,
    },
  },
];

export const FORBIDDEN_ROUTES: Routes = [
  {
    path: '',
    loadComponent: loadErrorPage,
    data: {
      code: '403',
      title: 'Pristup odbijen',
      message: 'Nemate dozvolu za pristup ovoj stranici sa trenutnim nalogom.',
    },
  },
];

export const SERVER_ERROR_ROUTES: Routes = [
  {
    path: '',
    loadComponent: loadErrorPage,
    data: {
      code: '500',
      title: 'Došlo je do greške',
      message: 'Nešto je pošlo po zlu na našoj strani. Pokušajte ponovo za nekoliko trenutaka.',
    },
  },
];
