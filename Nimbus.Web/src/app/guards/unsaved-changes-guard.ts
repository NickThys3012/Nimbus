import { CanDeactivateFn } from '@angular/router';

export interface CanComponentDeactivate {
  /** True while the component holds changes the user hasn't explicitly saved yet. */
  hasUnsavedChanges(): boolean;
}

/**
 * Generic `canDeactivate` guard: blocks navigating away while the active component reports
 * unsaved changes, unless the user confirms they want to discard them. Reusable by any
 * route/component that implements `CanComponentDeactivate` — flight preparation (issue #32) is
 * the first.
 *
 * Uses the native `confirm()` dialog as a minimal placeholder; swap for a proper `Nimbus-modal`
 * confirmation (see `pages/settings/balloons`) once a non-blocking variant of that pattern exists
 * for route guards.
 */
export const unsavedChangesGuard: CanDeactivateFn<CanComponentDeactivate> = (component) => {
  if (!component.hasUnsavedChanges()) return true;
  return confirm('You have unsaved changes. Leave this page and discard them?');
};
