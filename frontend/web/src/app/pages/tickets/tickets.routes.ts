import { Routes } from '@angular/router';

export const TICKETS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./tickets.component').then(m => m.TicketsComponent)
  }
];
