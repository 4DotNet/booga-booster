import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./ride-dashboard/ride-dashboard').then((m) => m.RideDashboard),
  },
  { path: '**', redirectTo: '' },
];
