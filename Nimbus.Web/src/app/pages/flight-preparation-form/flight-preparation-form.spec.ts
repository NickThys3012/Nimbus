import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';

import FlightPreparationFormPage from './flight-preparation-form';
import { AuthStore } from '../../core/auth/auth.store';
import {
  ApiError,
  BalloonsService,
  FlightAssessmentService,
  type FlightPreparationDto,
  FlightPreparationsService,
  LaunchLocationsService,
  LiftCalculationService,
  PilotsService,
  SolarService,
  WeatherService,
} from '../../core/api-client';
import { PAX_BRIEFING_TEMPLATE_HTML, PAX_BRIEFING_TEMPLATE_VERSION } from '../../core/flight-preparations/pax-briefing-template';
import { toRequestDto } from '../../core/flight-preparations/flight-preparation.mapper';
import { LOCAL_STORAGE } from '../../services/theme-service';

/**
 * Covers the issue #32 mockup-driven UI on top of the nine-section form (segmented progress bar,
 * dirty/draft badges, save-error banner, the async "Leave without saving?" dialog). The reactive
 * form's own validity/section-building behaviour is covered by
 * `core/flight-preparations/flight-preparation-form.spec.ts`-style unit tests; this file is about
 * what the page renders and wires up around it.
 */
describe('FlightPreparationFormPage', () => {
  let email: string | null;
  let mockStore: Record<string, string>;
  let mockStorage: Storage;
  let navigate: ReturnType<typeof vi.fn>;
  /** The flights "stored on the server" for this test, by id. */
  let server: Record<string, FlightPreparationDto>;
  let flightsApi: Record<string, ReturnType<typeof vi.fn>>;
  let weatherApi: { getApiWeatherMetarTaf: ReturnType<typeof vi.fn> };
  let solarApi: { getApiSolarSunTimes: ReturnType<typeof vi.fn> };

  const httpError = (status: number) =>
    new ApiError({ method: 'GET', url: '/api/flight-preparations' } as never, { url: '', ok: false, status, statusText: '', body: null }, `HTTP ${status}`);

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
    server = {};
    weatherApi = {
      getApiWeatherMetarTaf: vi.fn().mockReturnValue(of({ station: 'EBDM', metar: 'EBDM 100850Z 08003KT 0400 FG Q1013', taf: 'TAF EBDM 100800Z 1009/1018 09006KT 0500 FG BKN001\n  BECMG 1009/1011 6000 NSW SCT010' })),
    };
    solarApi = {
      getApiSolarSunTimes: vi.fn().mockReturnValue(
        of({ date: '2026-08-08', condition: 'normal', sunriseUtc: '', solarNoonUtc: '', sunsetUtc: '', timeZone: 'Europe/Brussels', sunrise: '06:12', solarNoon: '13:38', sunset: '21:04' }),
      ),
    };
    flightsApi = {
      getApiFlightPreparations: vi.fn().mockReturnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 20 })),
      getApiFlightPreparations1: vi.fn(({ id }: { id: string }) => (server[id] ? of(server[id]) : throwError(() => httpError(404)))),
      postApiFlightPreparations: vi.fn(({ requestBody }: { requestBody: never }) =>
        of({ id: 'server-1', status: 'Planned', updatedDate: '2026-08-09T09:00:00Z', details: requestBody })),
      putApiFlightPreparations: vi.fn(({ id, requestBody }: { id: string; requestBody: never }) =>
        of({ id, status: 'Planned', updatedDate: '2026-08-09T09:00:00Z', details: requestBody })),
    };
  });

  const balloon = {
    id: 'b1', registration: 'OO-ABC', type: 'X', envelopeVolumeM3: 3000, maxEnvelopeTemperatureC: 120,
    envelopeWeightKg: 118, basketWeightKg: 90, burnerWeightKg: 25, cylindersWeightKg: 60, emptyWeightKg: 293,
  };
  const location = { id: 'l1', name: 'Weelde', icaoCode: null, latitude: 51.4, longitude: 4.9, elevationM: null, airspaceNotes: null };

  async function createComponent(routeId: string | null = null, balloons: unknown[] = [], locations: unknown[] = []) {
    navigate = vi.fn().mockResolvedValue(true);

    await TestBed.configureTestingModule({
      imports: [FlightPreparationFormPage],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { email: () => email } },
        { provide: LOCAL_STORAGE, useValue: mockStorage },
        { provide: BalloonsService, useValue: { getApiBalloons: () => of(balloons) } },
        { provide: PilotsService, useValue: { getApiPilots: () => of([]) } },
        { provide: LiftCalculationService, useValue: { getApiLiftCalculationTotalLift: () => of({ totalLiftKg: 0 }) } },
        {
          provide: FlightAssessmentService,
          useValue: {
            postApiFlightAssessment: () =>
              of({ totalWeightKg: 0, passengersWeightKg: 0, marginKg: 0, liftSufficient: null, goNoGoVerdict: 'unknown' }),
          },
        },
        { provide: FlightPreparationsService, useValue: flightsApi },
        { provide: WeatherService, useValue: weatherApi },
        { provide: SolarService, useValue: solarApi },
        { provide: LaunchLocationsService, useValue: { getApiLaunchLocations: () => of(locations) } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap(routeId ? { id: routeId } : {}) },
            paramMap: of(convertToParamMap(routeId ? { id: routeId } : {})),
          },
        },
        { provide: Router, useValue: { navigate } },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(FlightPreparationFormPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement>(id: string) => el.querySelector(`[data-testid="${id}"]`) as T | null;
    const click = async (id: string) => {
      const target = q(id);
      (target?.querySelector('button') ?? target)?.dispatchEvent(new Event('click', { bubbles: true }));
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    const expandSection = async (id: string) => {
      const header = el.querySelector(`[data-section="${id}"] .card-head`) as HTMLButtonElement | null;
      header?.click();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    return { fixture, el, q, click, expandSection };
  }

  function seedSavedFlight(id: string, overrides: Record<string, unknown> = {}) {
    const baseline = {
      general: {
        flightDate: '2026-08-08', flightTime: '18:45', balloonId: 'b1', pilotId: 'p1',
        launchLocationId: 'l1', landingLocationId: '', sunriseTime: '', sunsetTime: '', fieldOwnerNotified: false,
      },
      meteo: {
        windSpeedKt: 8, windDirectionDeg: 240, visibilityKm: 10, capeJPerKg: 0, temperatureC: 0,
        dewPointC: 0, qnhHpa: 1018, metar: '', taf: '', windLevels: [],
      },
      airspace: { notamsChecked: true, airspaceStructureNotes: '', airspaceRestrictions: '', obstacles: '' },
      checklists: {
        firstAidAndExtinguisher: 'No', passengerListFilled: 'No', flightPlanFiled: 'No', burnerTested: false,
        gasCylindersChecked: false, balloonVisualInspectionDone: false, mooringsChecked: false, instrumentsChecked: false,
      },
      loadCalculation: {
        envelopeWeightKg: 118, basketWeightKg: 0, burnerWeightKg: 0, cylindersWeightKg: 0, maxAltitudeFt: 4500,
        liftUnits: 0, totalLiftKg: 0, loadRemarks: '', passengers: [],
      },
      route: { routeDescription: '', climbRateMetersPerSecond: 0, descentRateMetersPerSecond: 0, cruiseAltitudesFtCsv: '' },
      bulletin: { bulletinText: '' },
      ofp: { ofpOperatorName: 'Nimbus Balloons', ofpPicWeightKg: 82, ofpFuelAvailableMinutes: 0, ofpFuelRequiredMinutes: 0, ofpLandingLocation: '' },
      paxBriefing: { paxBriefingText: '', paxBriefingVersion: '', paxBriefingIsCustom: false },
      ...overrides,
    };
    server[id] = { id, status: 'Planned', updatedDate: '2026-08-08T17:42:00.000Z', details: toRequestDto(baseline as never, 'Weelde · 2026-08-08') };
    return baseline;
  }

  it('renders one progress segment per section and a completion counter', async () => {
    const { q } = await createComponent();

    const segments = q('progress-segments')!.querySelectorAll('.segment');
    expect(segments.length).toBe(9);
    expect(q('progress-count')?.textContent).toContain('/9 complete');
  });

  it('defaults a new flight to the next sensible date and time instead of empty', async () => {
    const { q } = await createComponent();

    expect(q<HTMLInputElement>('flight-date')?.value).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(q<HTMLInputElement>('flight-time')?.value).toMatch(/^(07|19):00$/);
  });

  it('starts a new flight with the standard passenger briefing, not customized', async () => {
    const { fixture } = await createComponent();

    expect(fixture.componentInstance['form']().controls.paxBriefing.getRawValue()).toEqual({
      paxBriefingText: PAX_BRIEFING_TEMPLATE_HTML,
      paxBriefingVersion: PAX_BRIEFING_TEMPLATE_VERSION,
      paxBriefingIsCustom: false,
    });
    expect(fixture.componentInstance['form']().dirty).toBe(false);
  });

  it('keeps the stored briefing of a saved flight instead of replacing it with the template', async () => {
    seedSavedFlight('flight-1', { paxBriefing: { paxBriefingText: '<p>my own</p>', paxBriefingVersion: 'old', paxBriefingIsCustom: true } });
    const { fixture } = await createComponent('flight-1');

    expect(fixture.componentInstance['form']().controls.paxBriefing.getRawValue()).toEqual({
      paxBriefingText: '<p>my own</p>',
      paxBriefingVersion: 'old',
      paxBriefingIsCustom: true,
    });
  });

  it('snapshots the picked balloon weights onto a new flight', async () => {
    const { fixture, q } = await createComponent(null, [balloon]);
    const select = q<HTMLSelectElement>('balloon')!;
    select.value = 'b1';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const load = fixture.componentInstance['form']().controls.loadCalculation.getRawValue();
    expect(load.envelopeWeightKg).toBe(118);
    expect(load.basketWeightKg).toBe(90);
    expect(load.burnerWeightKg).toBe(25);
    expect(load.cylindersWeightKg).toBe(60);
  });

  it('does not overwrite the weight snapshot of an already saved flight when its balloon changes', async () => {
    seedSavedFlight('flight-1', { loadCalculation: { envelopeWeightKg: 100, basketWeightKg: 11, burnerWeightKg: 12, cylindersWeightKg: 13, maxAltitudeFt: 4500, liftUnits: 0, totalLiftKg: 0, loadRemarks: '', passengers: [] } });
    const { fixture, q } = await createComponent('flight-1', [balloon]);
    const select = q<HTMLSelectElement>('balloon')!;
    select.value = 'b1';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(fixture.componentInstance['form']().controls.loadCalculation.getRawValue()).toMatchObject({
      envelopeWeightKg: 100, basketWeightKg: 11, burnerWeightKg: 12, cylindersWeightKg: 13,
    });
  });

  it('loads a saved flight\'s weight snapshot as stored, without making the form dirty', async () => {
    seedSavedFlight('flight-1', { loadCalculation: { envelopeWeightKg: 100, basketWeightKg: 11, burnerWeightKg: 12, cylindersWeightKg: 13, maxAltitudeFt: 4500, liftUnits: 0, totalLiftKg: 0, loadRemarks: '', passengers: [] } });
    const { fixture } = await createComponent('flight-1', [balloon]);

    const form = fixture.componentInstance['form']();
    expect(form.controls.loadCalculation.getRawValue()).toMatchObject({ basketWeightKg: 11, burnerWeightKg: 12, cylindersWeightKg: 13 });
    expect(form.dirty).toBe(false);
  });

  it('exposes the selected launch location coordinates and offers a landing location', async () => {
    const { fixture, q } = await createComponent(null, [], [location]);
    expect(q('launch-coordinates')).toBeNull();
    expect(q('landing-location')).not.toBeNull();

    const select = q<HTMLSelectElement>('launch-location')!;
    select.value = 'l1';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(fixture.componentInstance.launchCoordinates()).toEqual({ latitude: 51.4, longitude: 4.9 });
    expect(q('launch-coordinates')?.textContent).toContain('51.4');
  });

  describe('sunrise and sunset from the NOAA calculation (issue #49, form mockup)', () => {
    async function pickLocation(withDate = '2026-08-08') {
      const ctx = await createComponent(null, [], [location]);
      const date = ctx.q<HTMLInputElement>('flight-date')!;
      date.value = withDate;
      date.dispatchEvent(new Event('input'));
      const select = ctx.q<HTMLSelectElement>('launch-location')!;
      select.value = 'l1';
      select.dispatchEvent(new Event('change'));
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      ctx.fixture.detectChanges();
      return ctx;
    }

    it('fills Sunrise and Sunset for the launch location and the flight date', async () => {
      const { q } = await pickLocation();

      expect(solarApi.getApiSolarSunTimes).toHaveBeenCalledWith(expect.objectContaining({ latitude: 51.4, longitude: 4.9, date: '2026-08-08' }));
      expect(q<HTMLInputElement>('sunrise')!.value).toBe('06:12');
      expect(q<HTMLInputElement>('sunset')!.value).toBe('21:04');
      expect(q('sun-note')?.textContent).toContain('NOAA');
    });

    it('keeps a time the pilot typed', async () => {
      const ctx = await createComponent(null, [], [location]);
      const sunrise = ctx.q<HTMLInputElement>('sunrise')!;
      sunrise.value = '05:50';
      sunrise.dispatchEvent(new Event('input'));
      const date = ctx.q<HTMLInputElement>('flight-date')!;
      date.value = '2026-08-08';
      date.dispatchEvent(new Event('input'));
      const select = ctx.q<HTMLSelectElement>('launch-location')!;
      select.value = 'l1';
      select.dispatchEvent(new Event('change'));
      ctx.fixture.detectChanges();
      await ctx.fixture.whenStable();
      ctx.fixture.detectChanges();

      expect(ctx.q<HTMLInputElement>('sunrise')!.value).toBe('05:50');
      expect(ctx.q<HTMLInputElement>('sunset')!.value).toBe('21:04');
    });

    it('calculates nothing until a launch location is picked', async () => {
      const { q } = await createComponent(null, [], [location]);

      expect(solarApi.getApiSolarSunTimes).not.toHaveBeenCalled();
      expect(q('sun-note')).toBeNull();
    });
  });

  describe('METAR/TAF fetch in the Meteo header (issue #44, form mockup)', () => {
    const withIcao = { ...location, id: 'l2', name: 'Dendermonde', icaoCode: 'EBDM' };

    async function open() {
      const ctx = await createComponent(null, [], [location, withIcao]);
      const pick = async (id: string) => {
        const select = ctx.q<HTMLSelectElement>('launch-location')!;
        select.value = id;
        select.dispatchEvent(new Event('change'));
        ctx.fixture.detectChanges();
        await ctx.fixture.whenStable();
        ctx.fixture.detectChanges();
      };
      const button = () => ctx.q<HTMLButtonElement>('fetch-metar-taf')!;
      const flush = async () => {
        ctx.fixture.detectChanges();
        await ctx.fixture.whenStable();
        ctx.fixture.detectChanges();
      };
      const meteoSummary = () => ctx.el.querySelector('[data-section="Meteo"] [data-testid="section-summary"]')?.textContent?.trim() ?? null;
      return { ...ctx, pick, button, flush, meteoSummary };
    }

    it('puts a "Fetch" button with a refresh icon in the section header, beside, not inside, the toggle, even while collapsed', async () => {
      const { el, button } = await open();

      expect(el.querySelector('[data-section="Meteo"] .card-body')).toBeNull();
      expect(button()).not.toBeNull();
      expect(button().textContent?.trim()).toBe('Fetch');
      expect(button().querySelector('.ti-refresh')).not.toBeNull();
      expect(button().closest('.card-header')).not.toBeNull();
      expect(button().closest('.card-head')).toBeNull();
      expect(el.querySelectorAll('[data-testid="fetch-metar-taf"]').length).toBe(1);
    });

    it('is not offered on any other section', async () => {
      const { el } = await open();
      expect(el.querySelectorAll('[data-section]:not([data-section="Meteo"]) .card-action').length).toBe(0);
    });

    it('is blocked, and shows why once opened, until the launch location has an ICAO code', async () => {
      const { button, pick, flush, q, el } = await open();

      expect(button().getAttribute('aria-disabled')).toBe('true');
      expect(button().getAttribute('title')).toContain('Pick a launch location');

      await pick('l1');
      expect(button().getAttribute('aria-disabled')).toBe('true');
      expect(button().getAttribute('title')).toContain('no ICAO code');

      button().click(); // blocked, but still answers: the section opens to show the reason
      await flush();
      expect(el.querySelector('[data-section="Meteo"] .card-body')).not.toBeNull();
      expect(q('fetch-blocked')?.textContent).toContain('no ICAO code');
      expect(weatherApi.getApiWeatherMetarTaf).not.toHaveBeenCalled();

      await pick('l2');
      expect(button().getAttribute('aria-disabled')).toBeNull();
      expect(q('fetch-blocked')).toBeNull();
    });

    it('fetches for the picked location, opens the section and shows the result there', async () => {
      const { button, pick, flush, q, el } = await open();
      await pick('l2');

      button().click();
      await flush();

      expect(weatherApi.getApiWeatherMetarTaf).toHaveBeenCalledWith({ icao: 'EBDM' });
      expect(el.querySelector('[data-section="Meteo"] .card-body')).not.toBeNull();
      expect(q<HTMLTextAreaElement>('metar')?.value).toBe('EBDM 100850Z 08003KT 0400 FG Q1013');
      expect(q<HTMLTextAreaElement>('taf')?.value.split('\n')).toHaveLength(2);
      expect(q('fetch-ok')?.textContent).toContain('Fetched the METAR and TAF for EBDM');
    });

    it('shows the METAR station and observation time in the header, as in the mockup', async () => {
      const { button, pick, flush, meteoSummary } = await open();
      await pick('l2');
      expect(meteoSummary()).toBeNull();

      button().click();
      await flush();

      expect(meteoSummary()).toBe('EBDM · 08:50Z');
    });

    it('shows it for a saved flight too, read from its METAR text, not remembered from a fetch', async () => {
      seedSavedFlight('flight-1', { meteo: { windSpeedKt: 8, windDirectionDeg: 240, visibilityKm: 10, capeJPerKg: 0, temperatureC: 0, dewPointC: 0, qnhHpa: 1018, metar: 'EBAW 081750Z 24006KT 9999 FEW035 19/11 Q1018', taf: '', windLevels: [] } });
      const { el } = await createComponent('flight-1');

      expect(el.querySelector('[data-section="Meteo"] [data-testid="section-summary"]')?.textContent?.trim()).toBe('EBAW · 17:50Z');
    });

    it('falls back to the wind in the header while the METAR is empty or free text', async () => {
      seedSavedFlight('flight-1');
      const { el } = await createComponent('flight-1');

      expect(el.querySelector('[data-section="Meteo"] [data-testid="section-summary"]')?.textContent?.trim()).toBe('8 kt · 240°');
    });

    it('shows a provider failure in the section and leaves what the pilot typed alone', async () => {
      const { button, pick, flush, q, expandSection } = await open();
      await pick('l2');
      await expandSection('Meteo');
      const metar = q<HTMLTextAreaElement>('metar')!;
      metar.value = 'my own METAR';
      metar.dispatchEvent(new Event('input'));
      weatherApi.getApiWeatherMetarTaf.mockReturnValue(throwError(() => httpError(502)));

      button().click();
      await flush();

      expect(q('fetch-failed')?.textContent).toContain('Your entries are unchanged');
      expect(q<HTMLTextAreaElement>('metar')?.value).toBe('my own METAR');
    });
  });

  it('shows a save-error banner naming the invalid sections and expands the first one when saving an empty form', async () => {
    const { q, click } = await createComponent();

    await click('save-flight-prep');

    const banner = q('save-error-banner');
    expect(banner?.textContent).toContain('Not saved.');
    expect(banner?.textContent).toContain('General');
    // The first invalid section (General) must be auto-expanded.
    expect(q('flight-date')).not.toBeNull();
  });

  it('marks a section dirty ("EDITED") once an existing flight is edited, and reflects it in the sticky-bar summary', async () => {
    seedSavedFlight('flight-1');
    const { fixture, el, q, expandSection } = await createComponent('flight-1');

    expect(q('section-badge')).toBeNull();
    expect(q('dirty-summary')).toBeNull();

    await expandSection('Meteo');
    const windSpeed = el.querySelector<HTMLInputElement>('[data-testid="wind-speed"]');
    windSpeed!.value = '12';
    windSpeed!.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(q('dirty-summary')?.textContent).toContain('Meteo');
  });

  it('shows a draft-restored banner with a "FROM DRAFT" badge when a newer autosave differs from the saved baseline, and "Discard draft" clears it', async () => {
    const baseline = seedSavedFlight('flight-1');
    const autosaveValue = { ...baseline, meteo: { ...baseline.meteo, windSpeedKt: 20 } };
    mockStore['nimbus.flight-prep.autosave.pilot.a@example.com.flight-1'] = JSON.stringify({
      value: autosaveValue,
      updatedAt: '2026-08-08T18:02:00.000Z',
    });

    const { q, click } = await createComponent('flight-1');

    expect(q('draft-restored-banner')).not.toBeNull();
    expect(q('section-badge')?.textContent).toContain('FROM DRAFT');

    await click('discard-draft');

    expect(q('draft-restored-banner')).toBeNull();
    expect(q('section-badge')).toBeNull();
  });

  describe('saving through the API', () => {
    /** Fills the minimum that makes every section valid, as a pilot would. */
    function fillValid(fixture: { componentInstance: FlightPreparationFormPage }) {
      const form = fixture.componentInstance['form']();
      form.controls.general.patchValue({ flightDate: '2026-08-08', flightTime: '18:45', balloonId: 'b1', pilotId: 'p1', launchLocationId: 'l1' });
      form.controls.airspace.patchValue({ notamsChecked: true });
      form.controls.ofp.patchValue({ ofpOperatorName: 'Nimbus Balloons' });
      form.markAsDirty();
    }

    it('creates a new flight on the server, titled by place and date, then moves to its real id and refreshes the list', async () => {
      const { fixture, q, click } = await createComponent(null, [balloon], [location]);
      fillValid(fixture);
      // The sunrise/sunset calculation (issue #49) fills its fields once it answers; let it, so it is saved with the rest.
      await fixture.whenStable();
      fixture.detectChanges();

      await click('save-flight-prep');

      expect(flightsApi['postApiFlightPreparations']).toHaveBeenCalledTimes(1);
      expect(flightsApi['putApiFlightPreparations']).not.toHaveBeenCalled();
      const body = flightsApi['postApiFlightPreparations'].mock.calls[0][0].requestBody;
      expect(body.title).toBe('Weelde · 2026-08-08');
      expect(body.balloonId).toBe('b1');
      expect(body.paxBriefingText).toBe(PAX_BRIEFING_TEMPLATE_HTML);
      expect(body.paxBriefingIsCustom).toBe(false);
      expect(navigate).toHaveBeenCalledWith(['/flight-preparations', 'server-1'], { replaceUrl: true });
      expect(q('saved-tag')).not.toBeNull();
      expect(fixture.componentInstance['form']().dirty).toBe(false);
      expect(fixture.componentInstance['isExistingFlight']()).toBe(true);
    });

    it('updates (not creates) a flight the server already has', async () => {
      seedSavedFlight('flight-1');
      const { fixture, click } = await createComponent('flight-1', [balloon], [location]);
      fixture.componentInstance['form']().controls.meteo.patchValue({ windSpeedKt: 12 });

      await click('save-flight-prep');

      expect(flightsApi['putApiFlightPreparations']).toHaveBeenCalledTimes(1);
      expect(flightsApi['putApiFlightPreparations'].mock.calls[0][0].id).toBe('flight-1');
      expect(flightsApi['postApiFlightPreparations']).not.toHaveBeenCalled();
      expect(navigate).not.toHaveBeenCalled();
    });

    it('drops the local autosave once the flight is stored', async () => {
      const { fixture, click } = await createComponent(null, [balloon], [location]);
      mockStore['nimbus.flight-prep.autosave.pilot.a@example.com.new'] = '{}';
      fillValid(fixture);

      await click('save-flight-prep');

      expect(mockStore['nimbus.flight-prep.autosave.pilot.a@example.com.new']).toBeUndefined();
    });

    it('keeps the form dirty and shows a failure banner when the server request fails', async () => {
      flightsApi['postApiFlightPreparations'].mockReturnValue(throwError(() => httpError(500)));
      const { fixture, q, click } = await createComponent(null, [balloon], [location]);
      fillValid(fixture);

      await click('save-flight-prep');

      expect(q('save-failure-banner')?.textContent).toContain('Could not save');
      expect(fixture.componentInstance['form']().dirty).toBe(true);
      expect(fixture.componentInstance['isExistingFlight']()).toBe(false);
      expect(navigate).not.toHaveBeenCalled();
    });

    it('does not call the API for an invalid dossier', async () => {
      const { click } = await createComponent();

      await click('save-flight-prep');

      expect(flightsApi['postApiFlightPreparations']).not.toHaveBeenCalled();
    });

    it('treats an id the server does not know as an unsaved draft, so saving creates it', async () => {
      const { fixture, click } = await createComponent('draft-uuid', [balloon], [location]);
      fillValid(fixture);

      await click('save-flight-prep');

      expect(flightsApi['postApiFlightPreparations']).toHaveBeenCalledTimes(1);
    });

    it('sends a user who may only view the flight (403) to the read-only view instead of showing an editor', async () => {
      flightsApi['getApiFlightPreparations1'].mockReturnValue(throwError(() => httpError(403)));
      const { q } = await createComponent('flight-1', [balloon], [location]);

      expect(navigate).toHaveBeenCalledWith(['/flights', 'flight-1'], { replaceUrl: true });
      expect(q('load-error-banner')).toBeNull();
      expect(flightsApi['postApiFlightPreparations']).not.toHaveBeenCalled();
    });

    it('blocks saving when the stored flight could not be loaded, instead of overwriting or duplicating it', async () => {
      flightsApi['getApiFlightPreparations1'].mockReturnValue(throwError(() => httpError(500)));
      const { fixture, q, click } = await createComponent('flight-1', [balloon], [location]);
      fillValid(fixture);

      expect(q('load-error-banner')).not.toBeNull();
      await click('save-flight-prep');

      expect(flightsApi['postApiFlightPreparations']).not.toHaveBeenCalled();
      expect(flightsApi['putApiFlightPreparations']).not.toHaveBeenCalled();
      expect(q('save-failure-banner')?.textContent).toContain('Reload the page');
    });
  });

  describe('"Leave without saving?" dialog (unsaved-changes guard contract)', () => {
    it('opens with the destination and dirty section names, and "stay" resolves false without navigating', async () => {
      seedSavedFlight('flight-1');
      const { fixture, el, q, click, expandSection } = await createComponent('flight-1');
      const component = fixture.componentInstance;

      await expandSection('Meteo');
      const windSpeed = el.querySelector<HTMLInputElement>('[data-testid="wind-speed"]');
      windSpeed!.value = '12';
      windSpeed!.dispatchEvent(new Event('input'));

      let resolved: boolean | undefined;
      component.confirmDiscard('/logbook').subscribe((leave) => (resolved = leave));
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(q('going-to')?.textContent).toContain('/logbook');
      expect(q('leave-copy')?.textContent).toContain('Meteo');

      await click('stay-on-page');

      expect(resolved).toBe(false);
    });

    it('"Discard changes and leave" resolves true without saving', async () => {
      seedSavedFlight('flight-1');
      const { fixture, el, click, expandSection } = await createComponent('flight-1');
      const component = fixture.componentInstance;

      await expandSection('Meteo');
      const windSpeed = el.querySelector<HTMLInputElement>('[data-testid="wind-speed"]');
      windSpeed!.value = '12';
      windSpeed!.dispatchEvent(new Event('input'));

      let resolved: boolean | undefined;
      component.confirmDiscard('/logbook').subscribe((leave) => (resolved = leave));
      fixture.detectChanges();
      await fixture.whenStable();

      await click('leave-without-saving');

      expect(resolved).toBe(true);
      expect(flightsApi['putApiFlightPreparations']).not.toHaveBeenCalled();
    });

    it('"Save and leave" saves first and only resolves true once the dossier is valid', async () => {
      seedSavedFlight('flight-1');
      const { fixture, click } = await createComponent('flight-1');
      const component = fixture.componentInstance;

      let resolved: boolean | undefined;
      component.confirmDiscard('/logbook').subscribe((leave) => (resolved = leave));
      fixture.detectChanges();
      await fixture.whenStable();

      await click('save-and-leave');

      expect(resolved).toBe(true);
      expect(flightsApi['putApiFlightPreparations']).toHaveBeenCalledTimes(1);
    });
  });
});
