import { Routes } from '@angular/router';

export const PRODUCT_DETAILS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./product-details.component').then((m) => m.ProductDetailsComponent),
  },
];
