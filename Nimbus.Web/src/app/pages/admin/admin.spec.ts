import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import Admin from './admin';
import { AdminService, type AdminUserDto, type LoginEventDto } from '../../core/api-client';
import { AuthStore } from '../../core/auth/auth.store';

const base = { firstName: 'X', lastName: 'Y', isBlocked: false, requestedAt: '2026-07-26T10:00:00Z' };
const users: AdminUserDto[] = [
  { ...base, id: 'p1', email: 'pending@example.com', firstName: 'Piper', lastName: 'Pending', isApproved: false, isRejected: false, roles: ['Pilot'] },
  { ...base, id: 'me', email: 'admin@example.com', firstName: 'Ada', lastName: 'Admin', isApproved: true, isRejected: false, roles: ['Admin'] },
  { ...base, id: 'u1', email: 'pilot@example.com', firstName: 'Pia', lastName: 'Pilot', isApproved: true, isRejected: false, roles: ['Pilot'] },
  { ...base, id: 'b1', email: 'blocked@example.com', isApproved: true, isRejected: false, isBlocked: true, roles: ['Pilot'] },
  { ...base, id: 'r1', email: 'rejected@example.com', isApproved: false, isRejected: true, roles: ['Pilot'] },
];
const events: LoginEventDto[] = [
  { occurredAt: '2026-07-28T09:14:00', userEmail: 'admin@example.com', succeeded: true, maskedIp: '84.198.x.x' },
  { occurredAt: '2026-07-27T22:41:00', userEmail: null, succeeded: false, maskedIp: '45.227.x.x' },
];

describe('Admin', () => {
  let api: Record<string, Mock>;

  async function setup(list: AdminUserDto[] = users) {
    api = {
      getApiAdminUsers: vi.fn().mockReturnValue(of(list)),
      getApiAdminLoginEvents: vi.fn().mockReturnValue(of(events)),
      postApiAdminUsersApprove: vi.fn().mockReturnValue(of(undefined)),
      postApiAdminUsersReject: vi.fn().mockReturnValue(of(undefined)),
      putApiAdminUsersRole: vi.fn().mockReturnValue(of(undefined)),
      postApiAdminUsersBlock: vi.fn().mockReturnValue(of(undefined)),
      postApiAdminUsersUnblock: vi.fn().mockReturnValue(of(undefined)),
      deleteApiAdminUsers: vi.fn().mockReturnValue(of(undefined)),
    };
    await TestBed.configureTestingModule({
      imports: [Admin],
      providers: [
        { provide: AdminService, useValue: api },
        { provide: AuthStore, useValue: { currentUser: { value: () => ({ id: 'me' }) } } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(Admin);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const open = async (id: string) => {
      (el.querySelector(`#tab-${id}`) as HTMLButtonElement).click();
      await fixture.whenStable();
    };
    const click = async (testId: string) => {
      (el.querySelector(`[data-testid="${testId}"] button`) as HTMLButtonElement).click();
      await fixture.whenStable();
    };
    return { fixture, el, open, click };
  }

  afterEach(() => vi.restoreAllMocks());

  it('shows the three tabs with badges', async () => {
    const { el } = await setup();
    expect(el.querySelector('#tab-pending')?.textContent).toContain('In afwachting');
    expect(el.querySelector('#tab-pending Nimbus-count-badge')?.textContent?.trim()).toBe('1');
    expect(el.querySelector('#tab-users Nimbus-count-badge')?.textContent?.trim()).toBe('5');
    expect(el.querySelector('#tab-history')?.textContent).toContain('Aanmeldgeschiedenis');
  });

  it('lists only pending users (with request date) on the first tab', async () => {
    const { el } = await setup();
    expect(el.textContent).toContain('pending@example.com');
    expect(el.textContent).toContain('aangevraagd 26 jul');
    expect(el.textContent).not.toContain('rejected@example.com');
    expect(el.querySelector('[data-testid="approve-me"]')).toBeNull();
  });

  it('approves and reloads', async () => {
    const { click } = await setup();
    await click('approve-p1');
    expect(api['postApiAdminUsersApprove']).toHaveBeenCalledWith({ id: 'p1' });
    expect(api['getApiAdminUsers']).toHaveBeenCalledTimes(2);
  });

  it('rejects and reloads', async () => {
    const { click } = await setup();
    await click('reject-p1');
    expect(api['postApiAdminUsersReject']).toHaveBeenCalledWith({ id: 'p1' });
    expect(api['getApiAdminUsers']).toHaveBeenCalledTimes(2);
  });

  it('shows the empty state when nobody is pending', async () => {
    const { el } = await setup([users[1]]);
    expect(el.textContent).toContain('Geen aanvragen in afwachting.');
  });

  it('shows an error when an action fails', async () => {
    const { el, click } = await setup();
    api['postApiAdminUsersApprove'].mockReturnValue(throwError(() => new Error('boom')));
    await click('approve-p1');
    expect(el.querySelector('[role="alert"]')?.textContent).toContain('Actie mislukt');
  });

  it('users tab: role select for others, chip for self, status pills', async () => {
    const { el, open } = await setup();
    await open('users');
    expect(el.querySelector('[data-testid="role-u1"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="role-me"]')).toBeNull();
    expect(el.querySelector('[data-testid="user-me"] Nimbus-role-chip')?.textContent).toContain('admin');
    expect(el.querySelector('[data-testid="user-p1"]')?.textContent).toContain('in afwachting');
    expect(el.querySelector('[data-testid="user-u1"]')?.textContent).toContain('goedgekeurd');
    expect(el.querySelector('[data-testid="user-b1"]')?.textContent).toContain('geblokkeerd');
    expect(el.querySelector('[data-testid="user-r1"]')?.textContent).toContain('geweigerd');
  });

  it('users tab: no block/delete for yourself; delete only for rejected', async () => {
    const { el, open } = await setup();
    await open('users');
    expect(el.querySelector('[data-testid="block-me"]')).toBeNull();
    expect(el.querySelector('[data-testid="delete-me"]')).toBeNull();
    expect(el.querySelector('[data-testid="delete-u1"]')).toBeNull();
    expect(el.querySelector('[data-testid="delete-r1"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="block-r1"]')).toBeNull();
  });

  it('changes a role via the select', async () => {
    const { fixture, el, open } = await setup();
    await open('users');
    const select = el.querySelector('[data-testid="role-u1"]') as HTMLSelectElement;
    select.value = 'Admin';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(api['putApiAdminUsersRole']).toHaveBeenCalledWith({ id: 'u1', requestBody: { role: 'Admin' } });
  });

  it('blocks after confirmation, unblocks directly', async () => {
    const { el, open, click } = await setup();
    await open('users');
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
    await click('block-u1');
    expect(api['postApiAdminUsersBlock']).not.toHaveBeenCalled();
    confirmSpy.mockReturnValue(true);
    await click('block-u1');
    expect(api['postApiAdminUsersBlock']).toHaveBeenCalledWith({ id: 'u1' });
    await click('block-b1');
    expect(api['postApiAdminUsersUnblock']).toHaveBeenCalledWith({ id: 'b1' });
    expect(el.querySelector('[data-testid="block-b1"] i.ti-lock-open')).toBeTruthy();
  });

  it('deletes a rejected user after confirmation', async () => {
    const { open, click } = await setup();
    await open('users');
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    await click('delete-r1');
    expect(api['deleteApiAdminUsers']).toHaveBeenCalledWith({ id: 'r1' });
  });

  it('history tab loads and renders the login events lazily', async () => {
    const { el, open } = await setup();
    expect(api['getApiAdminLoginEvents']).not.toHaveBeenCalled();
    await open('history');
    expect(api['getApiAdminLoginEvents']).toHaveBeenCalledWith({ limit: 100 });
    expect(el.textContent).toContain('aangemeld');
    expect(el.textContent).toContain('mislukte poging');
    expect(el.textContent).toContain('onbekend');
    expect(el.textContent).toContain('84.198.x.x');
  });
});
