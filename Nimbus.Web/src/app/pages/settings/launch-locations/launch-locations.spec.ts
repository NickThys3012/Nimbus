import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import LaunchLocations from './launch-locations';
import { ApiError, type LaunchLocationDto, LaunchLocationsService } from '../../../core/api-client';

vi.mock('leaflet', () => {
  const map = { setView: () => map, remove: () => undefined, getContainer: () => null };
  const layer = { addTo: () => layer };
  return { map: () => map, tileLayer: () => layer, divIcon: () => ({}), marker: () => ({ addTo: () => layer, setLatLng: () => undefined }) };
});

const dender: LaunchLocationDto = { id: 'l1', name: 'Dendermonde', icaoCode: 'EBDM', latitude: 51.02, longitude: 4.1, elevationM: 8, airspaceNotes: 'Class G' };
const kluis: LaunchLocationDto = { id: 'l2', name: 'Kluisbos', icaoCode: null, latitude: 50.8, longitude: 3.5, elevationM: 120, airspaceNotes: null };

describe('LaunchLocations', () => {
  let api: Record<string, Mock>;

  async function setup(list: LaunchLocationDto[] = [dender, kluis]) {
    api = {
      getApiLaunchLocations: vi.fn().mockReturnValue(of(list)),
      postApiLaunchLocations: vi.fn().mockReturnValue(of(dender)),
      putApiLaunchLocations: vi.fn().mockReturnValue(of(dender)),
      deleteApiLaunchLocations: vi.fn().mockReturnValue(of(undefined)),
    };
    await TestBed.configureTestingModule({
      imports: [LaunchLocations],
      providers: [{ provide: LaunchLocationsService, useValue: api }],
    }).compileComponents();
    const fixture = TestBed.createComponent(LaunchLocations);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement>(id: string) => el.querySelector(`[data-testid="${id}"]`) as T;
    const dialog = () => el.querySelector('[role="alertdialog"]') as HTMLElement | null;
    const click = async (id: string) => {
      const target = q(id);
      (target.querySelector('button') ?? target).click();
      await fixture.whenStable();
    };
    const type = async (id: string, value: string) => {
      const input = q<HTMLInputElement>(id);
      input.value = value;
      input.dispatchEvent(new Event('input'));
      await fixture.whenStable();
    };
    const fill = async (v: { name?: string; icao?: string; lat?: string; lon?: string; elev?: string; notes?: string }) => {
      if (v.name !== undefined) await type('name', v.name);
      if (v.icao !== undefined) await type('icaoCode', v.icao);
      if (v.lat !== undefined) await type('latitude', v.lat);
      if (v.lon !== undefined) await type('longitude', v.lon);
      if (v.elev !== undefined) await type('elevationM', v.elev);
      if (v.notes !== undefined) await type('airspaceNotes', v.notes);
    };
    return { fixture, el, q, dialog, click, type, fill };
  }

  afterEach(() => vi.restoreAllMocks());

  it('lists locations in API order with ICAO badge, coordinates and elevation', async () => {
    const { el, q } = await setup();
    const names = Array.from(el.querySelectorAll('.nm')).map((n) => n.firstChild?.textContent?.trim());
    expect(names).toEqual(['Dendermonde', 'Kluisbos']);
    expect(el.querySelectorAll('[data-testid="icao"]')).toHaveLength(1);
    expect(q('location-l1').textContent).toContain('EBDM');
    expect(q('location-l1').textContent).toContain('51.02, 4.1');
    expect(q('location-l1').textContent).toContain('8 m');
    expect(q('subtitle').textContent?.trim()).toBe('2 LOCATIONS · A–Z · DRIVES WEATHER & SUNRISE');
  });

  it('shows an empty state with an Add location button', async () => {
    const { el, q, click } = await setup([]);
    expect(el.textContent).toContain('No launch locations yet');
    await click('add-location-empty');
    expect(q('location-form')).toBeTruthy();
    expect(el.querySelector('[role="dialog"]')?.textContent).toContain('Add launch location');
  });

  it('validates required fields, coordinate ranges and the ICAO code client side', async () => {
    const { q, click, fill } = await setup();
    await click('add-location');
    await click('save-location');
    expect(api['postApiLaunchLocations']).not.toHaveBeenCalled();
    expect(q('name-error')).toBeTruthy();
    expect(q('latitude-error')).toBeTruthy();
    expect(q('longitude-error')).toBeTruthy();
    expect(q('elevation-error')).toBeTruthy();

    await fill({ name: 'X', lat: '91', lon: '181', elev: '5', icao: 'EBD' });
    await click('save-location');
    expect(api['postApiLaunchLocations']).not.toHaveBeenCalled();
    expect(q('latitude-error').textContent).toContain('between -90 and 90');
    expect(q('longitude-error').textContent).toContain('between -180 and 180');
    expect(q('icao-error').textContent).toContain('four letters or digits');
  });

  it('accepts decimal degrees, uppercases the ICAO code and sends blank optionals as null', async () => {
    const { click, fill } = await setup();
    await click('add-location');
    await fill({ name: '  Field ', icao: 'ebdm', lat: '-33.865143', lon: '151.2099', elev: '12.5', notes: '  ' });
    await click('save-location');
    expect(api['postApiLaunchLocations']).toHaveBeenCalledWith({
      requestBody: { name: 'Field', icaoCode: 'EBDM', latitude: -33.865143, longitude: 151.2099, elevationM: 12.5, airspaceNotes: null },
    });
    expect(api['getApiLaunchLocations']).toHaveBeenCalledTimes(2);
  });

  it('omits the ICAO code when left empty', async () => {
    const { click, fill } = await setup();
    await click('add-location');
    await fill({ name: 'Field', lat: '51', lon: '4', elev: '5' });
    await click('save-location');
    expect(api['postApiLaunchLocations'].mock.calls[0][0].requestBody.icaoCode).toBeNull();
  });

  it('shows the preview map only for valid coordinates', async () => {
    const { q, click, fill, el } = await setup();
    await click('add-location');
    expect(q('location-map-empty')).toBeTruthy();
    await fill({ lat: '51.02', lon: '4.1' });
    await new Promise((r) => setTimeout(r));
    expect(q('location-map')).toBeTruthy();
    await fill({ lat: '151' });
    expect(q('location-map')).toBeNull();
    expect(el.querySelector('[data-testid="location-map-empty"]')).toBeTruthy();
  });

  it('edits an existing location with its values prefilled', async () => {
    const { q, click, type, el } = await setup();
    await click('edit-l1');
    expect(el.querySelector('[role="dialog"]')?.textContent).toContain('Edit launch location');
    expect(q<HTMLInputElement>('name').value).toBe('Dendermonde');
    expect(q<HTMLInputElement>('icaoCode').value).toBe('EBDM');
    expect(q<HTMLInputElement>('latitude').value).toBe('51.02');
    expect(q<HTMLTextAreaElement>('airspaceNotes').value).toBe('Class G');
    await type('elevationM', '9');
    await click('save-location');
    expect(api['putApiLaunchLocations']).toHaveBeenCalledWith({
      id: 'l1',
      requestBody: { name: 'Dendermonde', icaoCode: 'EBDM', latitude: 51.02, longitude: 4.1, elevationM: 9, airspaceNotes: 'Class G' },
    });
  });

  it('shows server-side field errors under the right field and the rest in a general alert', async () => {
    const { q, click, fill } = await setup();
    api['postApiLaunchLocations'].mockReturnValue(
      throwError(
        () =>
          new ApiError(
            { method: 'POST', url: '/x' },
            { url: '/x', ok: false, status: 422, statusText: '', body: { errors: { 'LaunchLocation.Latitude': ['Too precise.'], Other: ['Odd.'] } } },
            'err',
          ),
      ),
    );
    await click('add-location');
    await fill({ name: 'X', lat: '51', lon: '4', elev: '5' });
    await click('save-location');
    expect(q('latitude-server-error').textContent).toContain('Too precise.');
    expect(q('form-error').textContent).toContain('Odd.');
    expect(q('location-form')).toBeTruthy();
  });

  it('confirms before deleting, and cancel leaves the location alone', async () => {
    const { click, dialog } = await setup();
    await click('delete-l1');
    expect(dialog()?.textContent).toContain('Delete Dendermonde?');
    expect(api['deleteApiLaunchLocations']).not.toHaveBeenCalled();
    await click('dlg-cancel');
    expect(dialog()).toBeNull();
    await click('delete-l1');
    await click('dlg-delete');
    expect(api['deleteApiLaunchLocations']).toHaveBeenCalledWith({ id: 'l1' });
    expect(dialog()).toBeNull();
  });

  it('opens the three-dots menu with edit and delete', async () => {
    const { q, click, dialog } = await setup();
    await click('menu-l1');
    expect(q('menu-edit-l1')).toBeTruthy();
    await click('menu-delete-l1');
    expect(dialog()?.textContent).toContain('Delete Dendermonde?');
  });
});
