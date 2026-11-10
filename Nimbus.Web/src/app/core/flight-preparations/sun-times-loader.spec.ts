import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { vi } from 'vitest';

import { SolarService, type SunTimesDto } from '../api-client';
import { SunTimesLoader, type SunControl } from './sun-times-loader';

const times = (over: Partial<SunTimesDto> = {}): SunTimesDto => ({
  date: '2026-08-08',
  condition: 'normal',
  sunriseUtc: '2026-08-08T04:12:00Z',
  solarNoonUtc: '2026-08-08T11:38:00Z',
  sunsetUtc: '2026-08-08T19:04:00Z',
  timeZone: 'Europe/Brussels',
  sunrise: '06:12',
  solarNoon: '13:38',
  sunset: '21:04',
  ...over,
});

const control = (value = ''): SunControl & { dirty: boolean } => ({
  value,
  dirty: false,
  setValue(v: string) {
    this.value = v;
  },
  markAsDirty() {
    this.dirty = true;
  },
});

describe('SunTimesLoader (issue #49)', () => {
  function setup(api = vi.fn().mockReturnValue(of(times()))) {
    TestBed.configureTestingModule({ providers: [SunTimesLoader, { provide: SolarService, useValue: { getApiSolarSunTimes: api } }] });
    const loader = TestBed.inject(SunTimesLoader);
    const flush = async () => {
      TestBed.tick();
      await Promise.resolve();
      await Promise.resolve();
      TestBed.tick();
    };
    return { loader, api, flush };
  }

  it('asks for the launch location and the flight date, in the pilot\'s time zone', async () => {
    const { loader, api, flush } = setup();
    loader.timeZone.set('Europe/Brussels');
    loader.coordinates.set({ latitude: 51.4, longitude: 4.95 });
    loader.date.set('2026-08-08');
    await flush();

    expect(api).toHaveBeenCalledWith({ latitude: 51.4, longitude: 4.95, date: '2026-08-08', timeZone: 'Europe/Brussels' });
    expect(loader.times()?.sunrise).toBe('06:12');
    expect(loader.note()).toContain('NOAA');
  });

  it('asks for nothing until there is a location and a full date', async () => {
    const { loader, api, flush } = setup();
    loader.date.set('2026-08-08');
    await flush();
    loader.coordinates.set({ latitude: 51.4, longitude: 4.95 });
    loader.date.set('2026-08-');
    await flush();

    expect(api).not.toHaveBeenCalled();
    expect(loader.times()).toBeNull();
    expect(loader.note()).toBeNull();
  });

  it('says so, and keeps the fields free, when the calculation fails', async () => {
    const { loader, flush } = setup(vi.fn().mockReturnValue(throwError(() => new Error('down'))));
    loader.coordinates.set({ latitude: 51.4, longitude: 4.95 });
    loader.date.set('2026-08-08');
    await flush();

    expect(loader.times()).toBeNull();
    expect(loader.note()).toContain('Enter them by hand');
  });

  it('ignores an answer for a date that was replaced meanwhile', async () => {
    const first = new Subject<SunTimesDto>();
    const api = vi.fn().mockReturnValueOnce(first).mockReturnValueOnce(of(times({ date: '2026-08-09', sunrise: '06:14', sunset: '21:02' })));
    const { loader, flush } = setup(api);
    loader.coordinates.set({ latitude: 51.4, longitude: 4.95 });
    loader.date.set('2026-08-08');
    await flush();
    loader.date.set('2026-08-09');
    await flush();
    first.next(times());
    first.complete();
    await flush();

    expect(loader.times()?.sunrise).toBe('06:14');
  });

  it('explains polar day and polar night', async () => {
    const { loader, flush } = setup(vi.fn().mockReturnValue(of(times({ condition: 'polarDay' }))));
    loader.coordinates.set({ latitude: 69.6, longitude: 19 });
    loader.date.set('2026-06-21');
    await flush();

    expect(loader.note()).toContain('Polar day');
    loader.fill(loader.times(), control(), control()); // no edge values are written into the fields
  });

  describe('fill', () => {
    it('fills empty fields and marks them edited', () => {
      const { loader } = setup();
      const rise = control();
      const set = control();

      loader.fill(times(), rise, set);

      expect([rise.value, set.value]).toEqual(['06:12', '21:04']);
      expect(rise.dirty && set.dirty).toBe(true);
    });

    it('never overwrites a time the pilot typed', () => {
      const { loader } = setup();
      const rise = control('05:55');
      const set = control();

      loader.fill(times(), rise, set);

      expect(rise.value).toBe('05:55');
      expect(set.value).toBe('21:04');
    });

    it('refreshes what it wrote itself when the date or place changes', () => {
      const { loader } = setup();
      const rise = control();
      const set = control();
      loader.fill(times(), rise, set);

      loader.fill(times({ sunrise: '06:14', sunset: '21:02' }), rise, set);

      expect([rise.value, set.value]).toEqual(['06:14', '21:02']);
    });

    it('writes nothing for polar day or night, or when there are no times', () => {
      const { loader } = setup();
      const rise = control();
      const set = control();

      loader.fill(null, rise, set);
      loader.fill(times({ condition: 'polarNight' }), rise, set);
      loader.fill(times({ sunrise: null, sunset: null }), rise, set);

      expect([rise.value, set.value]).toEqual(['', '']);
      expect(rise.dirty).toBe(false);
    });
  });
});
