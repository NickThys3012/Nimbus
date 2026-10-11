import { Injectable } from '@angular/core';

/**
 * Keeps unsaved form state across a re-authentication round trip (session expired mid-edit →
 * login → back to the form). Lives in sessionStorage: it survives the in-tab navigation to
 * /login and back, but is dropped with the tab and never shared across tabs. Every access is
 * guarded because storage can be unavailable (private mode, blocked site data).
 */
@Injectable({ providedIn: 'root' })
export class DraftService {
  private static readonly prefix = 'nimbus.draft.';

  save(key: string, value: unknown): void {
    try {
      sessionStorage.setItem(DraftService.prefix + key, JSON.stringify(value));
    } catch {
      // Best effort: losing the draft must never break the form.
    }
  }

  load<T>(key: string): T | null {
    try {
      const raw = sessionStorage.getItem(DraftService.prefix + key);
      return raw === null ? null : (JSON.parse(raw) as T);
    } catch {
      return null;
    }
  }

  clear(key: string): void {
    try {
      sessionStorage.removeItem(DraftService.prefix + key);
    } catch {
      // Nothing to clean up.
    }
  }
}
