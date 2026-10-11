import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';

import { adminGuard } from './admin-guard';
import { AuthStore } from '../core/auth/auth.store';

describe('adminGuard', () => {
  let role: string | null;
  const run = () =>
    TestBed.runInInjectionContext(() =>
      adminGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );

  beforeEach(() => {
    role = null;
    TestBed.configureTestingModule({
      providers: [{ provide: AuthStore, useValue: { role: () => role } }],
    });
  });

  it('allows admins', () => {
    role = 'Admin';
    expect(run()).toBe(true);
  });

  it('sends non-admins to access-denied', () => {
    role = 'Pilot';
    expect(TestBed.inject(Router).serializeUrl(run() as UrlTree)).toBe('/access-denied');
  });
});
