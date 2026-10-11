import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
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
        { provide: AuthStore, useValue: { user: () => ({ id: 'me' }) } },
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
    expect(el.querySelector('#tab-pending')?.textContent).toContain('Pending');
    expect(el.querySelector('#tab-pending Nimbus-count-badge')?.textContent?.trim()).toBe('1');
    expect(el.querySelector('#tab-users Nimbus-count-badge')?.textContent?.trim()).toBe('5');
    expect(el.querySelector('#tab-history')?.textContent).toContain('Sign-in history');
  });

  it('links each tab to its tabpanel (aria-controls / aria-labelledby) and shows only the active one', async () => {
    const { el, open } = await setup();
    for (const id of ['pending', 'users', 'history']) {
      expect(el.querySelector(`#tab-${id}`)?.getAttribute('aria-controls')).toBe(`panel-${id}`);
      expect(el.querySelector(`#panel-${id}`)?.getAttribute('aria-labelledby')).toBe(`tab-${id}`);
      expect(el.querySelector(`#panel-${id}`)?.getAttribute('role')).toBe('tabpanel');
    }
    expect((el.querySelector('#panel-pending') as HTMLElement).hidden).toBe(false);
    expect((el.querySelector('#panel-users') as HTMLElement).hidden).toBe(true);
    await open('users');
    expect((el.querySelector('#panel-users') as HTMLElement).hidden).toBe(false);
    expect((el.querySelector('#panel-pending') as HTMLElement).hidden).toBe(true);
  });

  it('lists only pending users (with request date) on the first tab', async () => {
    const { el } = await setup();
    expect(el.textContent).toContain('pending@example.com');
    expect(el.textContent).toContain('requested 26 Jul');
    expect(el.textContent).not.toContain('rejected@example.com');
    expect(el.querySelector('[data-testid="approve-me"]')).toBeNull();
  });

  it('approves and reloads', async () => {
    const { click } = await setup();
    await click('approve-p1');
    expect(api['postApiAdminUsersApprove']).toHaveBeenCalledWith({ id: 'p1' });
    expect(api['getApiAdminUsers']).toHaveBeenCalledTimes(2);
  });

  it('tracks busy state per user: overlapping actions do not clear each other', async () => {
    const first = new Subject<void>();
    const second = new Subject<void>();
    const { el, fixture } = await setup([
      users[0],
      { ...users[0], id: 'p2', email: 'second@example.com' },
    ]);
    api['postApiAdminUsersApprove'].mockImplementation(({ id }: { id: string }) => (id === 'p1' ? first : second));
    const btn = (id: string) => el.querySelector(`[data-testid="approve-${id}"] button`) as HTMLButtonElement;

    btn('p1').click();
    btn('p2').click();
    await fixture.whenStable();
    expect(btn('p1').disabled).toBe(true);
    expect(btn('p2').disabled).toBe(true);

    first.next();
    first.complete();
    await fixture.whenStable();
    // p2 is still in flight even though p1 finished.
    expect(btn('p2').disabled).toBe(true);
    second.next();
    second.complete();
    await fixture.whenStable();
    expect(el.querySelector('[data-testid="approve-p2"] button')?.hasAttribute('disabled')).toBe(false);
  });

  it('rejects and reloads', async () => {
    const { click } = await setup();
    await click('reject-p1');
    expect(api['postApiAdminUsersReject']).toHaveBeenCalledWith({ id: 'p1' });
    expect(api['getApiAdminUsers']).toHaveBeenCalledTimes(2);
  });

  it('shows the empty state when nobody is pending', async () => {
    const { el } = await setup([users[1]]);
    expect(el.textContent).toContain('No requests pending.');
  });

  it('shows an error when an action fails', async () => {
    const { el, click } = await setup();
    api['postApiAdminUsersApprove'].mockReturnValue(throwError(() => new Error('boom')));
    await click('approve-p1');
    expect(el.querySelector('[role="alert"]')?.textContent).toContain('Action failed');
  });

  it('users tab: role select for others, chip for self, status pills', async () => {
    const { el, open } = await setup();
    await open('users');
    expect(el.querySelector('[data-testid="role-u1"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="role-me"]')).toBeNull();
    expect(el.querySelector('[data-testid="user-me"] Nimbus-role-chip')?.textContent).toContain('admin');
    expect(el.querySelector('[data-testid="user-p1"]')?.textContent).toContain('pending');
    expect(el.querySelector('[data-testid="user-u1"]')?.textContent).toContain('approved');
    expect(el.querySelector('[data-testid="user-b1"]')?.textContent).toContain('blocked');
    expect(el.querySelector('[data-testid="user-r1"]')?.textContent).toContain('rejected');
  });

  it('users tab: no block for yourself; delete disabled for yourself, available for others', async () => {
    const { el, open } = await setup();
    await open('users');
    expect(el.querySelector('[data-testid="block-me"]')).toBeNull();
    expect((el.querySelector('[data-testid="delete-me"] button') as HTMLButtonElement).disabled).toBe(true);
    expect((el.querySelector('[data-testid="delete-u1"] button') as HTMLButtonElement).disabled).toBe(false);
    expect(el.querySelector('[data-testid="delete-r1"]')).toBeTruthy();
    expect(el.querySelector('[data-testid="user-r1"] Nimbus-role-chip')?.textContent).toContain('pilot');
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

  it('does not delete without confirmation, and never deletes yourself', async () => {
    const { open, click } = await setup();
    await open('users');
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    await click('delete-u1');
    await click('delete-me');
    expect(api['deleteApiAdminUsers']).not.toHaveBeenCalled();
  });

  it('history tab loads and renders the login events lazily', async () => {
    const { el, open } = await setup();
    expect(api['getApiAdminLoginEvents']).not.toHaveBeenCalled();
    await open('history');
    expect(api['getApiAdminLoginEvents']).toHaveBeenCalledWith({ limit: 100 });
    expect(el.textContent).toContain('signed in');
    expect(el.textContent).toContain('failed attempt');
    expect(el.textContent).toContain('unknown');
    expect(el.textContent).toContain('84.198.x.x');
  });
});
