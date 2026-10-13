import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import { ProfileStore } from './profile.store';
import { ApiError, ProfileService } from '../api-client';

const empty = { operatorName: null, picWeightKg: null };
const filled = { operatorName: 'Skyline Ballooning', picWeightKg: 82 };

const apiError = (status: number, b: unknown) =>
  new ApiError({ method: 'PUT', url: '/x' }, { url: '/x', ok: false, status, statusText: '', body: b }, 'err');

describe('ProfileStore', () => {
  let api: Record<string, Mock>;
  let store: ProfileStore;

  async function setup() {
    api = {
      getApiProfile: vi.fn().mockReturnValue(of(empty)),
      putApiProfile: vi.fn().mockReturnValue(of(filled)),
    };
    TestBed.configureTestingModule({ providers: [{ provide: ProfileService, useValue: api }] });
    store = TestBed.inject(ProfileStore);
    TestBed.tick();
    await new Promise((r) => setTimeout(r));
  }

  it('loads the (possibly empty) profile', async () => {
    await setup();
    expect(store.profile.value()).toEqual(empty);
  });

  it('saves and takes the response as the new value', async () => {
    await setup();
    expect(await store.save(filled)).toBe(true);
    expect(api['putApiProfile']).toHaveBeenCalledWith({ requestBody: filled });
    expect(store.profile.value()).toEqual(filled);
  });

  it('maps a 422 to per-field errors', async () => {
    await setup();
    api['putApiProfile'].mockReturnValue(throwError(() => apiError(422, { errors: { 'Profile.PicWeightKg': ['Enter a weight above 0 kg.'] } })));
    expect(await store.save({ operatorName: null, picWeightKg: 0 })).toBe(false);
    expect(store.fieldErrors().picWeightKg).toBe('Enter a weight above 0 kg.');
    expect(store.saveError()).toBeNull();
  });

  it('falls back to a generic error', async () => {
    await setup();
    api['putApiProfile'].mockReturnValue(throwError(() => new Error('boom')));
    expect(await store.save(filled)).toBe(false);
    expect(store.saveError()).toBe('Something went wrong. Please try again.');
  });
});
