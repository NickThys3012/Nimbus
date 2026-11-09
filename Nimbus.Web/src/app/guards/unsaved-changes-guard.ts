import { CanDeactivateFn } from '@angular/router';
import { Observable } from 'rxjs';

export interface CanComponentDeactivate {
  /** True while the component holds changes the user hasn't explicitly saved yet. */
  hasUnsavedChanges(): boolean;
  /**
   * Called only once `hasUnsavedChanges()` is true. The component owns the confirmation UI (e.g.
   * its own "Leave without saving?" modal, see `FlightPreparationFormPage` — issue #32 mockup
   * frame 2B) and emits once the user has made a choice: `true` to proceed with navigation
   * (discarding, or after having saved), `false` to stay on the page.
   */
  confirmDiscard(destinationUrl: string): Observable<boolean>;
}

/**
 * Generic `canDeactivate` guard: blocks navigating away while the active component reports
 * unsaved changes, unless the user confirms (via the component's own dialog) that they want to
 * leave. Reusable by any route/component that implements `CanComponentDeactivate` — flight
 * preparation (issue #32) is the first.
 */
export const unsavedChangesGuard: CanDeactivateFn<CanComponentDeactivate> = (component, _route, _state, nextState) => {
  if (!component.hasUnsavedChanges()) return true;
  return component.confirmDiscard(nextState.url);
};
