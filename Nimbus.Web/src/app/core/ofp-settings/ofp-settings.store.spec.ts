import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import { OfpSettingsStore } from './ofp-settings.store';
import { ApiError, type OfpSettingsDto, OfpSettingsService } from '../api-client';

const defaults: OfpSettingsDto = { equipmentWeightPerPassengerKg: 7, isCustom: false };
const custom: OfpSettingsDto = { equipmentWeightPerPassengerKg: 9, isCustom: true };

const apiError = (status: number, b: unknown) =>
  new ApiError({ method: 'PUT', url: '/x' }, { url: '/x', ok: false, status, statusText: '', body: b }, 'err');

describe('OfpSettingsStore', () => {
  let api: Record<string, Mock>;
  let store: OfpSettingsStore;

  async function setup() {
    api = {
      getApiOfpSettings: vi.fn().mockReturnValue(of(defaults)),
      putApiOfpSettings: vi.fn().mockReturnValue(of(custom)),
      deleteApiOfpSettings: vi.fn().mockReturnValue(of(defaults)),
    };
    TestBed.configureTestingModule({ providers: [{ provide: OfpSettingsService, useValue: api }] });
    store = TestBed.inject(OfpSettingsStore);
    TestBed.tick();
    await new Promise((r) => setTimeout(r));
  }

  it('loads the effective settings', async () => {
    await setup();
    expect(store.settings.value()).toEqual(defaults);
  });

  it('saves and takes the response as the new value', async () => {
    await setup();
    expect(await store.save({ equipmentWeightPerPassengerKg: 9 })).toBe(true);
    expect(api['putApiOfpSettings']).toHaveBeenCalledWith({ requestBody: { equipmentWeightPerPassengerKg: 9 } });
    expect(store.settings.value()).toEqual(custom);
  });

  it('resets to the default returned by the API', async () => {
    await setup();
    await store.save({ equipmentWeightPerPassengerKg: 9 });
    expect(await store.reset()).toBe(true);
    expect(store.settings.value()).toEqual(defaults);
  });

  it('maps 422 to the field and other failures to a general error', async () => {
    await setup();
    api['putApiOfpSettings'].mockReturnValue(throwError(() => apiError(422, { errors: { 'Settings.EquipmentWeightPerPassengerKg': ['Too big.'] } })));
    expect(await store.save({ equipmentWeightPerPassengerKg: 900 })).toBe(false);
    expect(store.fieldError()).toBe('Too big.');
    api['putApiOfpSettings'].mockReturnValue(throwError(() => new Error('boom')));
    expect(await store.save({ equipmentWeightPerPassengerKg: 9 })).toBe(false);
    expect(store.fieldError()).toBeNull();
    expect(store.saveError()).toBe('Something went wrong. Please try again.');
  });

  it('reports a failed reset', async () => {
    await setup();
    api['deleteApiOfpSettings'].mockReturnValue(throwError(() => new Error('boom')));
    expect(await store.reset()).toBe(false);
    expect(store.saveError()).toBe('Something went wrong. Please try again.');
  });
});
