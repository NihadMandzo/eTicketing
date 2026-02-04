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
        loadChildren: () => import('./pages/events/events.routes').then(m => m.EVENTS_ROUTES)
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
      }
    ]
  }
];
