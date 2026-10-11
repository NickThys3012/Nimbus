import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import type { Observable } from 'rxjs';

import { Button } from '../../components/button/button';
import { EmptyState } from '../../components/empty-state/empty-state';
import { InitialsAvatar } from '../../components/initials-avatar/initials-avatar';
import { RoleChip } from '../../components/role-chip/role-chip';
import { StatusPill } from '../../components/status-pill/status-pill';
import { type TabItem, Tabs } from '../../components/tabs/tabs';
import { AdminService, ApiError, type AdminUserDto } from '../../core/api-client';
import { AuthStore } from '../../core/auth/auth.store';
import { formatDayMonth, formatDayMonthTime } from '../../core/format/dates';

type AdminTab = 'pending' | 'users' | 'history';

const ROLE_LABELS: Record<string, string> = { Admin: 'admin', Pilot: 'pilot' };
/** The roles that exist in the API (seeded from the UserRole enum). */
const ASSIGNABLE_ROLES = ['Admin', 'Pilot'];

@Component({
  selector: 'Nimbus-admin',
  imports: [Button, EmptyState, InitialsAvatar, RoleChip, StatusPill, Tabs],
  templateUrl: './admin.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './admin.css',
})
export default class Admin {
  private readonly adminApi = inject(AdminService);
  private readonly authStore = inject(AuthStore);

  protected readonly tab = signal<AdminTab>('pending');
  /** Ids of the users with an action in flight; each user is tracked independently. */
  protected readonly busyIds = signal<ReadonlySet<string>>(new Set());
  protected readonly actionError = signal<string | null>(null);
  protected readonly roles = ASSIGNABLE_ROLES;

  protected readonly users = rxResource({
    stream: () => this.adminApi.getApiAdminUsers(),
  });
  /** Only fetched while the history tab is open. */
  protected readonly events = rxResource({
    params: () => (this.tab() === 'history' ? true : undefined),
    stream: () => this.adminApi.getApiAdminLoginEvents({ limit: 100 }),
  });

  protected readonly allUsers = computed(() => (this.users.hasValue() ? this.users.value() : []));
  protected readonly pendingUsers = computed(() =>
    this.allUsers().filter((u) => !u.isApproved && !u.isRejected),
  );
  protected readonly myId = computed(() => this.authStore.currentUser.value()?.id ?? null);

  protected readonly tabs = computed<TabItem[]>(() => [
    { id: 'pending', label: 'Pending', count: this.pendingUsers().length, countTone: 'warn' },
    { id: 'users', label: 'Users', count: this.allUsers().length },
    { id: 'history', label: 'Sign-in history' },
  ]);

  protected isBusy(id: string): boolean {
    return this.busyIds().has(id);
  }

  private setBusy(id: string, busy: boolean): void {
    this.busyIds.update((ids) => {
      const next = new Set(ids);
      if (busy) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  protected setTab(id: string): void {
    this.tab.set(id as AdminTab);
  }

  protected isSelf(user: AdminUserDto): boolean {
    return user.id === this.myId();
  }

  protected displayName(user: AdminUserDto): string {
    const first = user.firstName?.trim();
    const last = user.lastName?.trim();
    if (first && last) return `${first[0].toUpperCase()}. ${last}`;
    return first || last || user.email;
  }

  protected initials(user: AdminUserDto): string {
    const first = user.firstName?.trim();
    const last = user.lastName?.trim();
    const value = `${first?.[0] ?? ''}${last?.[0] ?? ''}`.toUpperCase();
    return value || user.email.slice(0, 2).toUpperCase();
  }

  protected roleLabel(role: string): string {
    return ROLE_LABELS[role] ?? role.toLowerCase();
  }

  protected currentRole(user: AdminUserDto): string {
    return user.roles[0] ?? 'Pilot';
  }

  protected requestedOn(user: AdminUserDto): string {
    return user.requestedAt ? formatDayMonth(user.requestedAt) : '';
  }

  protected eventTime(iso: string): string {
    return formatDayMonthTime(iso);
  }

  protected approve(id: string): void {
    this.run(id, this.adminApi.postApiAdminUsersApprove({ id }));
  }

  protected reject(id: string): void {
    this.run(id, this.adminApi.postApiAdminUsersReject({ id }));
  }

  protected changeRole(user: AdminUserDto, event: Event): void {
    const role = (event.target as HTMLSelectElement).value;
    if (role === this.currentRole(user)) return;
    this.run(user.id, this.adminApi.putApiAdminUsersRole({ id: user.id, requestBody: { role } }));
  }

  protected toggleBlock(user: AdminUserDto): void {
    if (user.isBlocked) {
      this.run(user.id, this.adminApi.postApiAdminUsersUnblock({ id: user.id }));
    } else if (confirm(`Block ${user.email}? They will be signed out.`)) {
      this.run(user.id, this.adminApi.postApiAdminUsersBlock({ id: user.id }));
    }
  }

  protected remove(user: AdminUserDto): void {
    // The server rejects self-deletion too; this guard just keeps the UI honest.
    if (this.isSelf(user)) return;
    if (confirm(`Permanently delete ${user.email}? Their account, sessions and personal settings are removed. This cannot be undone.`)) {
      this.run(user.id, this.adminApi.deleteApiAdminUsers({ id: user.id }));
    }
  }

  private run(id: string, call: Observable<unknown>): void {
    if (this.isBusy(id)) return;
    this.setBusy(id, true);
    this.actionError.set(null);
    call.subscribe({
      next: () => {
        this.setBusy(id, false);
        this.users.reload();
      },
      error: (err: unknown) => {
        this.setBusy(id, false);
        const title = err instanceof ApiError && typeof err.body?.title === 'string' ? err.body.title : null;
        this.actionError.set(title ?? 'Action failed. Please try again.');
        // Re-sync the list so e.g. a rejected role select snaps back.
        this.users.reload();
      },
    });
  }
}
