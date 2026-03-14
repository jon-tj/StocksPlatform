import { inject } from '@angular/core';
import { Router, Routes } from '@angular/router';
import { AuthService } from './services/auth.service';

const authGuard = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn() ? true : router.createUrlTree(['/login']);
};

const loginGuard = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn() ? router.createUrlTree(['/dashboard']) : true;
};

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  {
    path: '',
    loadComponent: () => import('./layouts/app-layout/layout').then((m) => m.AppLayout),
    children: [
      {
        path: 'login',
        canActivate: [loginGuard],
        loadComponent: () => import('./pages/login/login').then((m) => m.Login),
      },
      {
        path: 'register',
        canActivate: [loginGuard],
        loadComponent: () => import('./pages/register/register').then((m) => m.Register),
      },
      {
        path: 'dashboard',
        canActivate: [authGuard],
        loadComponent: () => import('./pages/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'poll',
        canActivate: [authGuard],
        loadComponent: () => import('./pages/poll/poll').then((m) => m.Poll),
      },
      {
        path: 'analysis',
        canActivate: [authGuard],
        loadComponent: () => import('./pages/analysis/analysis').then((m) => m.Analysis),
      },
      {
        path: 'analysis/:asset',
        canActivate: [authGuard],
        loadComponent: () => import('./pages/analysis/analysis').then((m) => m.Analysis),
      },
    ],
  },
  { path: '**', redirectTo: 'login' },
];
