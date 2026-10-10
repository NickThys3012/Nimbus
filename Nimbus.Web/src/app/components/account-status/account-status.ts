import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router } from '@angular/router';

import { AuthStore } from '../../core/auth/auth.store';
import { formatDayMonthYear } from '../../core/format/dates';
import { Button } from '../button/button';
import { Glyph } from '../glyph/glyph';

export type AccountStatusKind = 'pending' | 'rejected';

const ROLE_LABELS: Record<string, string> = { Admin: 'admin', Pilot: 'pilot' };

/**
 * Full-page card shown instead of the app to a signed-in user an admin has not approved yet
 * ("pending") or has rejected ("rejected").
 */
@Component({
  selector: 'Nimbus-account-status',
  imports: [Glyph, Button],
  templateUrl: './account-status.html',
  styleUrl: './account-status.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountStatus {
  private readonly authStore = inject(AuthStore);
  private readonly router = inject(Router);

  status = input.required<AccountStatusKind>();

  protected readonly requestedOn = computed(() => {
    const iso = this.authStore.requestedAt();
    return iso ? formatDayMonthYear(iso) : null;
  });
  protected readonly roleLabel = computed(() => {
    const role = this.authStore.role();
    return role ? (ROLE_LABELS[role] ?? role.toLowerCase()) : null;
  });

  protected backToLogin(event: Event): void {
    event.preventDefault();
    this.authStore.logout();
    void this.router.navigate(['/login']);
  }
}
