import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';

import { AccountStatus } from './account-status';
import { AuthStore } from '../../core/auth/auth.store';

describe('AccountStatus', () => {
  const logout = vi.fn();
  const requestedAt = signal<string | null>('2026-07-26T10:00:00Z');

  async function setup(status: 'pending' | 'rejected') {
    logout.mockClear();
    await TestBed.configureTestingModule({
      imports: [AccountStatus],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { logout, requestedAt, role: () => 'Pilot' } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(AccountStatus);
    fixture.componentRef.setInput('status', status);
    await fixture.whenStable();
    return fixture;
  }

  it('pending: shows the card, request date, role and a mailto button', async () => {
    const el = (await setup('pending')).nativeElement as HTMLElement;
    expect(el.querySelector('#pending-approval-notice')).toBeTruthy();
    expect(el.textContent).toContain('Your account is awaiting approval');
    expect(el.querySelector('#meta-requested')?.textContent).toContain('26 Jul 2026');
    expect(el.querySelector('#meta-role')?.textContent).toContain('pilot');
    expect(el.querySelector('#contact-admin')?.getAttribute('href')).toMatch(/^mailto:/);
  });

  it('pending: omits the date line when the request date is unknown', async () => {
    requestedAt.set(null);
    const el = (await setup('pending')).nativeElement as HTMLElement;
    expect(el.querySelector('#meta-requested')).toBeNull();
    requestedAt.set('2026-07-26T10:00:00Z');
  });

  it('rejected: shows the no-access card without meta lines', async () => {
    const el = (await setup('rejected')).nativeElement as HTMLElement;
    expect(el.querySelector('#rejected-notice')).toBeTruthy();
    expect(el.textContent).toContain('No access to Nimbus');
    expect(el.querySelector('#meta-requested')).toBeNull();
    expect(el.querySelector('#contact-admin')).toBeTruthy();
  });

  it('logs out and returns to the login page via the back link', async () => {
    const fixture = await setup('pending');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    ((fixture.nativeElement as HTMLElement).querySelector('#back-to-login') as HTMLAnchorElement).click();
    expect(logout).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });
});
