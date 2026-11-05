import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { vi } from 'vitest';

import { unsavedChangesGuard } from './unsaved-changes-guard';

describe('unsavedChangesGuard', () => {
  const run = (hasUnsavedChanges: boolean) =>
    TestBed.runInInjectionContext(() =>
      unsavedChangesGuard(
        { hasUnsavedChanges: () => hasUnsavedChanges },
        {} as ActivatedRouteSnapshot,
        {} as RouterStateSnapshot,
        {} as RouterStateSnapshot,
      ),
    );

  it('allows navigation when there are no unsaved changes', () => {
    expect(run(false)).toBe(true);
  });

  it('confirms before discarding unsaved changes, and respects the answer', () => {
    const confirmSpy = vi.spyOn(globalThis, 'confirm').mockReturnValue(true);
    expect(run(true)).toBe(true);
    expect(confirmSpy).toHaveBeenCalledWith('You have unsaved changes. Leave this page and discard them?');

    confirmSpy.mockReturnValue(false);
    expect(run(true)).toBe(false);
  });
});
