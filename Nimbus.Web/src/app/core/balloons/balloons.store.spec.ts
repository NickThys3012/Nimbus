import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import { BalloonsStore } from './balloons.store';
import { ApiError, type BalloonDto, BalloonsService } from '../api-client';

const balloon: BalloonDto = {
  id: 'b1', registration: 'OO-ABC', type: 'T', envelopeVolumeM3: 1, maxEnvelopeTemperatureC: 1,
  envelopeWeightKg: 1, basketWeightKg: 1, burnerWeightKg: 1, cylindersWeightKg: 1, emptyWeightKg: 4,
};
const body = { registration: 'OO-ABC', type: '', envelopeVolumeM3: 1, maxEnvelopeTemperatureC: 1, envelopeWeightKg: 0, basketWeightKg: 0, burnerWeightKg: 0, cylindersWeightKg: 0 };

const apiError = (status: number, b: unknown) =>
  new ApiError({ method: 'POST', url: '/x' }, { url: '/x', ok: false, status, statusText: '', body: b }, 'err');

describe('BalloonsStore', () => {
  let api: Record<string, Mock>;
  let store: BalloonsStore;

  async function setup() {
    api = {
      getApiBalloons: vi.fn().mockReturnValue(of([balloon])),
      getApiBalloonsUsage: vi.fn().mockReturnValue(of({ flightCount: 0, recentFlights: [] })),
      postApiBalloons: vi.fn().mockReturnValue(of(balloon)),
      putApiBalloons: vi.fn().mockReturnValue(of(balloon)),
      deleteApiBalloons: vi.fn().mockReturnValue(of(undefined)),
    };
    TestBed.configureTestingModule({ providers: [{ provide: BalloonsService, useValue: api }] });
    store = TestBed.inject(BalloonsStore);
    TestBed.tick();
    await new Promise((r) => setTimeout(r));
  }

  it('loads the list', async () => {
    await setup();
    expect(store.list()).toEqual([balloon]);
    expect(store.hasLoaded()).toBe(true);
  });

  it('creates and updates, then reloads the list', async () => {
    await setup();
    expect(await store.save(null, body)).toBe(true);
    expect(api['postApiBalloons']).toHaveBeenCalledWith({ requestBody: body });
    expect(await store.save('b1', body)).toBe(true);
    expect(api['putApiBalloons']).toHaveBeenCalledWith({ id: 'b1', requestBody: body });
    expect(store.saving()).toBe(false);
  });

  it('flags a 409 as duplicate and other failures as an error message', async () => {
    await setup();
    api['postApiBalloons'].mockReturnValue(throwError(() => apiError(409, { title: 'dup' })));
    expect(await store.save(null, body)).toBe(false);
    expect(store.duplicate()).toBe(true);
    expect(store.saveError()).toBeNull();

    api['postApiBalloons'].mockReturnValue(throwError(() => apiError(422, { title: 'v', errors: { Registration: ['Too long.'] } })));
    expect(await store.save(null, body)).toBe(false);
    expect(store.duplicate()).toBe(false);
    expect(store.saveError()).toBe('Too long.');
  });

  it('looks up usage and reports a failed lookup', async () => {
    await setup();
    expect(await store.usage(balloon)).toEqual({ flightCount: 0, recentFlights: [] });
    expect(api['getApiBalloonsUsage']).toHaveBeenCalledWith({ id: 'b1' });
    expect(store.checkingId()).toBeNull();

    api['getApiBalloonsUsage'].mockReturnValue(throwError(() => apiError(404, { title: 'Not found' })));
    expect(await store.usage(balloon)).toBeNull();
    expect(store.listError()).toBe('Not found');
  });

  it('deletes, and surfaces the server refusal', async () => {
    await setup();
    expect(await store.remove('b1')).toBe(true);
    expect(api['deleteApiBalloons']).toHaveBeenCalledWith({ id: 'b1' });

    api['deleteApiBalloons'].mockReturnValue(throwError(() => apiError(409, { title: 'Used by 1 flight.' })));
    expect(await store.remove('b1')).toBe(false);
    expect(store.listError()).toBe('Used by 1 flight.');
    expect(store.deleting()).toBe(false);
  });
});
