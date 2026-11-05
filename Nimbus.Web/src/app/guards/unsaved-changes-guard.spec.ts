import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { firstValueFrom, Observable, of } from 'rxjs';
import { vi } from 'vitest';

import { unsavedChangesGuard } from './unsaved-changes-guard';

describe('unsavedChangesGuard', () => {
  const run = (hasUnsavedChanges: boolean, confirmDiscard: (destinationUrl: string) => Observable<boolean>) =>
    TestBed.runInInjectionContext(() =>
      unsavedChangesGuard(
        { hasUnsavedChanges: () => hasUnsavedChanges, confirmDiscard },
        {} as ActivatedRouteSnapshot,
        {} as RouterStateSnapshot,
        { url: '/logbook' } as RouterStateSnapshot,
      ),
    );

  it('allows navigation when there are no unsaved changes, without asking the component to confirm', () => {
    const confirmDiscard = vi.fn(() => of(true));
    expect(run(false, confirmDiscard)).toBe(true);
    expect(confirmDiscard).not.toHaveBeenCalled();
  });

  it('delegates the confirmation to the component, passing the destination URL', async () => {
    const confirmDiscard = vi.fn(() => of(true));
    const result = run(true, confirmDiscard) as Observable<boolean>;
    expect(confirmDiscard).toHaveBeenCalledWith('/logbook');
    expect(await firstValueFrom(result)).toBe(true);
  });

  it('respects the component resolving to stay on the page', async () => {
    const result = run(true, () => of(false)) as Observable<boolean>;
    expect(await firstValueFrom(result)).toBe(false);
  });
});
