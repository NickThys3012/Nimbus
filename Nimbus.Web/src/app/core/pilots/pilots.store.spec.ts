import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import { PilotsStore } from './pilots.store';
import { ApiError, type PilotDto, PilotsService } from '../api-client';

const pilot: PilotDto = { id: 'p1', name: 'N. Thys', weightKg: 82 };
const body = { name: 'N. Thys', weightKg: 82 };

const apiError = (status: number, b: unknown) =>
  new ApiError({ method: 'POST', url: '/x' }, { url: '/x', ok: false, status, statusText: '', body: b }, 'err');

describe('PilotsStore', () => {
  let api: Record<string, Mock>;
  let store: PilotsStore;

  async function setup() {
    api = {
      getApiPilots: vi.fn().mockReturnValue(of([pilot])),
      postApiPilots: vi.fn().mockReturnValue(of(pilot)),
      putApiPilots: vi.fn().mockReturnValue(of(pilot)),
      deleteApiPilots: vi.fn().mockReturnValue(of(undefined)),
    };
    TestBed.configureTestingModule({ providers: [{ provide: PilotsService, useValue: api }] });
    store = TestBed.inject(PilotsStore);
    TestBed.tick();
    await new Promise((r) => setTimeout(r));
  }

  it('loads the list', async () => {
    await setup();
    expect(store.list()).toEqual([pilot]);
  });

  it('creates and updates, then reloads the list', async () => {
    await setup();
    expect(await store.save(null, body)).toBe(true);
    expect(api['postApiPilots']).toHaveBeenCalledWith({ requestBody: body });
    expect(await store.save('p1', body)).toBe(true);
    expect(api['putApiPilots']).toHaveBeenCalledWith({ id: 'p1', requestBody: body });
    expect(store.saving()).toBe(false);
  });

  it('reports 422 messages and generic failures', async () => {
    await setup();
    api['postApiPilots'].mockReturnValue(throwError(() => apiError(422, { title: 'v', errors: { 'Pilot.WeightKg': ['Too heavy.'] } })));
    expect(await store.save(null, body)).toBe(false);
    expect(store.fieldErrors()).toEqual({ weightKg: 'Too heavy.' });
    expect(store.saveError()).toBeNull();

    api['postApiPilots'].mockReturnValue(throwError(() => apiError(422, { errors: { 'Pilot.Name': ['Bad.'], Weird: ['Odd.'] } })));
    expect(await store.save(null, body)).toBe(false);
    expect(store.fieldErrors()).toEqual({ name: 'Bad.' });
    expect(store.saveError()).toBe('Odd.');

    api['postApiPilots'].mockReturnValue(throwError(() => new Error('boom')));
    expect(await store.save(null, body)).toBe(false);
    expect(store.saveError()).toBe('Something went wrong. Please try again.');
    expect(store.fieldErrors()).toEqual({});
  });

  it('removes a pilot and surfaces a failure as the list error', async () => {
    await setup();
    expect(await store.remove('p1')).toBe(true);
    expect(api['deleteApiPilots']).toHaveBeenCalledWith({ id: 'p1' });

    api['deleteApiPilots'].mockReturnValue(throwError(() => apiError(404, { title: 'Not found' })));
    expect(await store.remove('p1')).toBe(false);
    expect(store.listError()).toBe('Not found');
  });
});
