import { TestBed } from '@angular/core/testing';

import { FlightPreparationDraftStore } from './flight-preparation-draft.store';
import { AuthStore } from '../auth/auth.store';
import { LOCAL_STORAGE } from '../../services/theme-service';

describe('FlightPreparationDraftStore', () => {
  let email: string | null;
  let store: FlightPreparationDraftStore;
  let mockStore: Record<string, string>;
  let mockStorage: Storage;

  function setup(): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthStore, useValue: { email: () => email } },
        { provide: LOCAL_STORAGE, useValue: mockStorage },
      ],
    });
    store = TestBed.inject(FlightPreparationDraftStore);
  }

  beforeEach(() => {
    mockStore = {};
    mockStorage = {
      getItem: (key: string) => mockStore[key] ?? null,
      setItem: (key: string, val: string) => { mockStore[key] = val; },
      removeItem: (key: string) => { delete mockStore[key]; },
      clear: () => { Object.keys(mockStore).forEach((k) => delete mockStore[k]); },
      key: (index: number) => Object.keys(mockStore)[index] ?? null,
      get length() { return Object.keys(mockStore).length; },
    } as Storage;
    email = 'pilot.a@example.com';
    setup();
  });

  it('round-trips a draft and reports the save timestamp', () => {
    const savedAt = store.save('flight-1', { general: { flightDate: '2026-08-08' } });
    const loaded = store.load<{ general: { flightDate: string } }>('flight-1');
    expect(loaded?.value).toEqual({ general: { flightDate: '2026-08-08' } });
    expect(loaded?.updatedAt).toBe(savedAt);
  });

  it('removes a draft', () => {
    store.save('flight-1', { a: 1 });
    store.remove('flight-1');
    expect(store.load('flight-1')).toBeNull();
  });

  it('returns null for a draft that was never saved', () => {
    expect(store.load('unknown')).toBeNull();
  });

  it('returns null for corrupt data instead of throwing', () => {
    mockStorage.setItem('nimbus.flight-prep.pilot.a@example.com.flight-1', '{not json');
    expect(store.load('flight-1')).toBeNull();
  });

  it('generates distinct ids', () => {
    expect(store.newId()).not.toBe(store.newId());
  });

  describe('per-user isolation', () => {
    it("never lets user B read user A's draft, even with the same flight id", () => {
      store.save('flight-1', { note: "user A's dossier" });

      email = 'pilot.b@example.com';
      setup();

      expect(store.load('flight-1')).toBeNull();
    });

    it("never lets user B overwrite user A's draft under the same flight id", () => {
      store.save('flight-1', { note: "user A's dossier" });

      email = 'pilot.b@example.com';
      setup();
      store.save('flight-1', { note: "user B's dossier" });

      email = 'pilot.a@example.com';
      setup();

      expect(store.load<{ note: string }>('flight-1')?.value).toEqual({ note: "user A's dossier" });
    });
  });
});
