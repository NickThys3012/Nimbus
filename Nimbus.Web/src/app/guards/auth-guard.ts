import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, of } from 'rxjs';

import { AuthStore } from '../core/auth/auth.store';

/**
 * Blocks routes to unauthenticated users. The access token only ever lives
 * in memory (see `AuthStore`), so on a fresh page load `isAuthenticated`
 * will be `false` even for a returning, still-logged-in user — before
 * denying access we attempt one silent `restoreSession()` via the httpOnly
 * refresh-token cookie. Otherwise the user is sent to login with the
 * requested URL as `returnUrl`. This is UX only: the API re-enforces access.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (authStore.isAuthenticated()) {
    return of(true);
  }

  return authStore
    .restoreSession()
    .pipe(
      map(
        (restored) =>
          restored || router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } }),
      ),
    );
};
