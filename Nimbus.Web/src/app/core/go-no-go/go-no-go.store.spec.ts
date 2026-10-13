import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import { GoNoGoStore } from './go-no-go.store';
import { ApiError, type GoNoGoThresholdsDto, GoNoGoThresholdsService } from '../api-client';

const defaults: GoNoGoThresholdsDto = { windYellowKt: 10, windRedKt: 15, visibilityYellowKm: 5, visibilityRedKm: 3, capeYellow: 300, capeRed: 500, isCustom: false };
const custom: GoNoGoThresholdsDto = { windYellowKt: 8, windRedKt: 12, visibilityYellowKm: 6, visibilityRedKm: 4, capeYellow: 250, capeRed: 400, isCustom: true };
const body = { windYellowKt: 8, windRedKt: 12, visibilityYellowKm: 6, visibilityRedKm: 4, capeYellow: 250, capeRed: 400 };

const apiError = (status: number, b: unknown) =>
  new ApiError({ method: 'PUT', url: '/x' }, { url: '/x', ok: false, status, statusText: '', body: b }, 'err');

describe('GoNoGoStore', () => {
  let api: Record<string, Mock>;
  let store: GoNoGoStore;

  async function setup() {
    api = {
      getApiGoNoGoThresholds: vi.fn().mockReturnValue(of(defaults)),
      putApiGoNoGoThresholds: vi.fn().mockReturnValue(of(custom)),
      deleteApiGoNoGoThresholds: vi.fn().mockReturnValue(of(defaults)),
    };
    TestBed.configureTestingModule({ providers: [{ provide: GoNoGoThresholdsService, useValue: api }] });
    store = TestBed.inject(GoNoGoStore);
    TestBed.tick();
    await new Promise((r) => setTimeout(r));
  }

  it('loads the effective thresholds', async () => {
    await setup();
    expect(store.thresholds.value()).toEqual(defaults);
  });

  it('saves and takes the response as the new value', async () => {
    await setup();
    expect(await store.save(body)).toBe(true);
    expect(api['putApiGoNoGoThresholds']).toHaveBeenCalledWith({ requestBody: body });
    expect(store.thresholds.value()).toEqual(custom);
    expect(store.saving()).toBe(false);
  });

  it('resets to the defaults returned by the API', async () => {
    await setup();
    await store.save(body);
    expect(await store.reset()).toBe(true);
    expect(store.thresholds.value()).toEqual(defaults);
  });

  it('maps 422 messages to fields, and other failures to a general error', async () => {
    await setup();
    api['putApiGoNoGoThresholds'].mockReturnValue(throwError(() => apiError(422, { errors: { 'Thresholds.WindRedKt': ['Must be higher than 8 kt.'] } })));
    expect(await store.save(body)).toBe(false);
    expect(store.fieldErrors()).toEqual({ windRedKt: 'Must be higher than 8 kt.' });
    expect(store.saveError()).toBeNull();

    api['putApiGoNoGoThresholds'].mockReturnValue(throwError(() => new Error('boom')));
    expect(await store.save(body)).toBe(false);
    expect(store.fieldErrors()).toEqual({});
    expect(store.saveError()).toBe('Something went wrong. Please try again.');
  });

  it('reports a failed reset', async () => {
    await setup();
    api['deleteApiGoNoGoThresholds'].mockReturnValue(throwError(() => new Error('boom')));
    expect(await store.reset()).toBe(false);
    expect(store.saveError()).toBe('Something went wrong. Please try again.');
  });
});
