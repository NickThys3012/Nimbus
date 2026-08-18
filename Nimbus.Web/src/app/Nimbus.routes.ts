import { Routes } from '@angular/router';
import { authGuard } from './guards/auth-guard';
import { adminGuard } from './guards/admin-guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'home',
  },
  {
    path: 'home',
    loadComponent: () => import('./pages/home/home'),
  },
  {
    path: 'flights',
    loadComponent: () => import('./pages/flight-overview/flight-overview'),
    canActivate: [authGuard],
  },
  {
    path: 'logbook',
    loadComponent: () => import('./pages/logbook/logbook'),
    canActivate: [authGuard],
  },
  {
    path: 'manual',
    loadComponent: () => import('./pages/manual/manual'),
    canActivate: [authGuard],
  },
  {
    path: 'admin',
    loadComponent: () => import('./pages/admin/admin'),
    canActivate: [authGuard, adminGuard],
  },
  {
    path: 'settings',
    loadComponent: () => import('./pages/settings/settings'),
    canActivate: [authGuard],
  },
  {
    path: 'find-user',
    loadComponent: () => import('./pages/user-by-mail/user-by-mail'),
  },
];
