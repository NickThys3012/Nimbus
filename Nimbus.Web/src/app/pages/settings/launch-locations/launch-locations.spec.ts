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
const kluis: LaunchLocationDto = { id: 'l2', name: 'Kluisbos', icaoCode: null, latitude: 50.8, longitude: 3.5, elevationM: null, airspaceNotes: null };

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

  it('lists locations with notes under the name, ICAO column, coordinates and elevation', async () => {
    const { el, q } = await setup();
    const names = Array.from(el.querySelectorAll('.nm')).map((n) => n.firstChild?.textContent?.trim());
    expect(names).toEqual(['Dendermonde', 'Kluisbos']);
    const heads = Array.from(el.querySelectorAll('[role="columnheader"]')).map((n) => n.textContent?.trim());
    expect(heads).toEqual(['NAME', 'ICAO', 'COORDINATES', 'ELEV.']);
    expect(q('location-l1').querySelector('.nm')?.textContent).toContain('Class G');
    expect(q('location-l1').querySelector('[data-testid="icao"]')?.textContent?.trim()).toBe('EBDM');
    expect(q('location-l1').textContent).toContain('51.02, 4.1');
    expect(q('location-l1').querySelector('[data-testid="elevation"]')?.textContent).toContain('8 m');
  });

  it('shows an em dash for a missing ICAO code or elevation', async () => {
    const { q } = await setup();
    expect(q('location-l2').querySelector('[data-testid="icao"]')?.textContent?.trim()).toBe('—');
    expect(q('location-l2').querySelector('[data-testid="elevation"]')?.textContent?.trim()).toBe('—');
  });

  it('shows the subtitle and the METAR/TAF footnote', async () => {
    const { q } = await setup();
    expect(q('subtitle').textContent?.trim()).toBe('2 LOCATIONS · DRIVE METAR/TAF, FORECAST & SUNRISE');
    expect(q('icao-footnote').textContent?.trim()).toBe('NO ICAO CODE → METAR/TAF LOOKUP SKIPPED FOR THAT LOCATION');
  });

  it('shows an empty state with an Add location button', async () => {
    const { el, q, click } = await setup([]);
    expect(el.textContent).toContain('No launch locations yet');
    expect(el.textContent).toContain('Weather and sunrise are then calculated automatically for that spot.');
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
    expect(q('elevation-error')).toBeNull(); // elevation is optional
    expect(q('name-error').textContent).toContain('Name is required.');
    expect(q('form-warning').textContent).toContain('Fix the highlighted fields before saving. The server validates these again.');

    await fill({ name: 'X', lat: '91', lon: '181', elev: '5', icao: 'EBD' });
    await click('save-location');
    expect(api['postApiLaunchLocations']).not.toHaveBeenCalled();
    expect(q('latitude-error').textContent).toContain('Between −90 and 90.');
    expect(q('longitude-error').textContent).toContain('Between −180 and 180.');
    expect(q('icao-error').textContent).toContain('ICAO code must be exactly 4 characters.');
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

  it('sends null for an empty ICAO code and an empty elevation', async () => {
    const { click, fill } = await setup();
    await click('add-location');
    await fill({ name: 'Field', lat: '51', lon: '4' });
    await click('save-location');
    const body = api['postApiLaunchLocations'].mock.calls[0][0].requestBody;
    expect(body.icaoCode).toBeNull();
    expect(body.elevationM).toBeNull();
  });

  it('shows the preview map only for valid coordinates', async () => {
    const { q, click, fill, el } = await setup();
    await click('add-location');
    expect(q('location-map-empty')).toBeTruthy();
    expect(q('location-map-reason').textContent).toBe('NO PIN · ENTER COORDINATES');
    expect(q('form-warning')).toBeNull();
    expect(el.querySelector('.preview')?.textContent).toContain('UPDATES AS YOU TYPE');
    await fill({ lat: '51.02', lon: '4.1' });
    await new Promise((r) => setTimeout(r));
    expect(q('location-map')).toBeTruthy();
    await fill({ lat: '151' });
    expect(q('location-map')).toBeNull();
    expect(el.querySelector('[data-testid="location-map-empty"]')).toBeTruthy();
    expect(q('location-map-reason').textContent).toBe('NO PIN · LATITUDE OUT OF RANGE');
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

  it('edits a location without elevation and keeps it empty', async () => {
    const { q, click } = await setup();
    await click('edit-l2');
    expect(q<HTMLInputElement>('elevationM').value).toBe('');
    await click('save-location');
    expect(api['putApiLaunchLocations'].mock.calls[0][0].requestBody.elevationM).toBeNull();
  });

  it('uses the wide dialog and the mockup labels and hints', async () => {
    const { el, click } = await setup();
    await click('add-location');
    expect(el.querySelector('.modal.w-wide')).toBeTruthy();
    const text = el.querySelector('[data-testid="location-form"]')?.textContent ?? '';
    for (const t of ['ICAO CODE (OPTIONAL)', '4 CHARACTERS · DRIVES METAR/TAF', 'DECIMAL DEGREES · LAT −90…90 · LON −180…180', 'ELEVATION (M AMSL)', 'AIRSPACE NOTES']) {
      expect(text).toContain(t);
    }
    expect(text).not.toContain('ELEVATION (M AMSL) *');
  });
});
