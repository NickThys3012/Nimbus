import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'find-user',
  },
  {
    path: 'find-user',
    loadComponent: () => import('./pages/user-by-mail/user-by-mail'),
  },
];
