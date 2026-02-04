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
        path: 'events',
        loadChildren: () => import('./pages/events/events.routes').then(m => m.EVENTS_ROUTES)
      },
      {
        path: 'tickets',
        loadChildren: () => import('./pages/tickets/tickets.routes').then(m => m.TICKETS_ROUTES)
      },
      {
        path: 'profile',
        loadChildren: () => import('./pages/profile/profile.routes').then(m => m.PROFILE_ROUTES)
      },
      {
        path: 'search',
        loadChildren: () => import('./pages/search/search.routes').then(m => m.SEARCH_ROUTES)
      },
      {
        path: 'cart',
        loadChildren: () => import('./pages/cart/cart.routes').then(m => m.CART_ROUTES)
      },
      {
        path: 'about',
        loadChildren: () => import('./pages/about/about.routes').then(m => m.ABOUT_ROUTES)
      }
    ]
  }
];
