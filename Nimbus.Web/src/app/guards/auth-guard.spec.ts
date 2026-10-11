import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { firstValueFrom, isObservable, of } from 'rxjs';

import { authGuard } from './auth-guard';
import { AuthStore } from '../core/auth/auth.store';

describe('authGuard', () => {
  let authenticated: boolean;
  let restored: boolean;

  const run = async () => {
    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, { url: '/flights?tab=1' } as RouterStateSnapshot),
    );
    return isObservable(result) ? firstValueFrom(result) : result;
  };

  beforeEach(() => {
    authenticated = false;
    restored = false;
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AuthStore,
          useValue: {
            isAuthenticated: () => authenticated,
            restoreSession: () => of(restored),
          },
        },
      ],
    });
  });

  it('allows an authenticated user', async () => {
    authenticated = true;
    expect(await run()).toBe(true);
  });

  it('allows a user whose session can be restored', async () => {
    restored = true;
    expect(await run()).toBe(true);
  });

  it('expired session: redirects to login carrying the requested URL', async () => {
    const result = (await run()) as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/login?returnUrl=%2Fflights%3Ftab%3D1');
  });
});
