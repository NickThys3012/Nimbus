import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthStore } from '../core/auth/auth.store';

/**
 * Blocks routes to non-admin users. Always list this guard *after*
 * `authGuard` in a route's `canActivate` array — Angular runs guards in
 * order, so by the time this one runs the session has already been
 * authenticated (or the user was already redirected away).
 */
export const adminGuard: CanActivateFn = () => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  return authStore.role() === 'Admin' || router.createUrlTree(['/home']);
};
