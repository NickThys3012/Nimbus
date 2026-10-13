import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import { LaunchLocationsStore } from './launch-locations.store';
import { ApiError, type LaunchLocationDto, LaunchLocationsService } from '../api-client';

const location: LaunchLocationDto = { id: 'l1', name: 'Dendermonde', icaoCode: 'EBDM', latitude: 51.02, longitude: 4.1, elevationM: 8, airspaceNotes: null };
const body = { name: location.name, icaoCode: location.icaoCode, latitude: location.latitude, longitude: location.longitude, elevationM: location.elevationM, airspaceNotes: location.airspaceNotes };

const apiError = (status: number, b: unknown) =>
  new ApiError({ method: 'POST', url: '/x' }, { url: '/x', ok: false, status, statusText: '', body: b }, 'err');

describe('LaunchLocationsStore', () => {
  let api: Record<string, Mock>;
  let store: LaunchLocationsStore;

  async function setup() {
    api = {
      getApiLaunchLocations: vi.fn().mockReturnValue(of([location])),
      postApiLaunchLocations: vi.fn().mockReturnValue(of(location)),
      putApiLaunchLocations: vi.fn().mockReturnValue(of(location)),
      deleteApiLaunchLocations: vi.fn().mockReturnValue(of(undefined)),
    };
    TestBed.configureTestingModule({ providers: [{ provide: LaunchLocationsService, useValue: api }] });
    store = TestBed.inject(LaunchLocationsStore);
    TestBed.tick();
    await new Promise((r) => setTimeout(r));
  }

  it('loads the list', async () => {
    await setup();
    expect(store.list()).toEqual([location]);
  });

  it('creates and updates, then reloads the list', async () => {
    await setup();
    expect(await store.save(null, body)).toBe(true);
    expect(api['postApiLaunchLocations']).toHaveBeenCalledWith({ requestBody: body });
    expect(await store.save('l1', body)).toBe(true);
    expect(api['putApiLaunchLocations']).toHaveBeenCalledWith({ id: 'l1', requestBody: body });
    expect(store.saving()).toBe(false);
  });

  it('reports 422 messages and generic failures', async () => {
    await setup();
    api['postApiLaunchLocations'].mockReturnValue(throwError(() => apiError(422, { title: 'v', errors: { 'LaunchLocation.Latitude': ['Too far north.'] } })));
    expect(await store.save(null, body)).toBe(false);
    expect(store.fieldErrors()).toEqual({ latitude: 'Too far north.' });
    expect(store.saveError()).toBeNull();

    api['postApiLaunchLocations'].mockReturnValue(throwError(() => apiError(422, { errors: { 'LaunchLocation.IcaoCode': ['Four chars.'] } })));
    expect(await store.save(null, body)).toBe(false);
    expect(store.fieldErrors()).toEqual({ icaoCode: 'Four chars.' });

    api['postApiLaunchLocations'].mockReturnValue(throwError(() => apiError(422, { errors: { 'LaunchLocation.Name': ['Bad.'], Weird: ['Odd.'] } })));
    expect(await store.save(null, body)).toBe(false);
    expect(store.fieldErrors()).toEqual({ name: 'Bad.' });
    expect(store.saveError()).toBe('Odd.');

    api['postApiLaunchLocations'].mockReturnValue(throwError(() => new Error('boom')));
    expect(await store.save(null, body)).toBe(false);
    expect(store.saveError()).toBe('Something went wrong. Please try again.');
    expect(store.fieldErrors()).toEqual({});
  });

  it('removes a location and surfaces a failure as the list error', async () => {
    await setup();
    expect(await store.remove('l1')).toBe(true);
    expect(api['deleteApiLaunchLocations']).toHaveBeenCalledWith({ id: 'l1' });

    api['deleteApiLaunchLocations'].mockReturnValue(throwError(() => apiError(404, { title: 'Not found' })));
    expect(await store.remove('l1')).toBe(false);
    expect(store.listError()).toBe('Not found');
  });
});
