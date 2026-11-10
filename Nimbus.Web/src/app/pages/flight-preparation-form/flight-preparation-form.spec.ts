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
        { provide: WeatherService, useValue: { getApiWeatherMetarTaf: vi.fn().mockReturnValue(of({ station: 'EBAW', metar: null, taf: null })) } },
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

  it('offers the METAR/TAF fetch for a launch location that has an ICAO code, and explains why it cannot without one', async () => {
    const withIcao = { ...location, id: 'l2', name: 'Dendermonde', icaoCode: 'EBDM' };
    const { fixture, q, expandSection } = await createComponent(null, [], [location, withIcao]);
    const pick = async (id: string) => {
      const select = q<HTMLSelectElement>('launch-location')!;
      select.value = id;
      select.dispatchEvent(new Event('change'));
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    const fetchButton = () => q('fetch-metar-taf')?.querySelector('button') as HTMLButtonElement;
    await expandSection('Meteo');

    expect(fetchButton().disabled).toBe(true);
    expect(q('fetch-blocked')?.textContent).toContain('Pick a launch location');

    await pick('l1');
    expect(fetchButton().disabled).toBe(true);
    expect(q('fetch-blocked')?.textContent).toContain('no ICAO code');

    await pick('l2');
    expect(fetchButton().disabled).toBe(false);
    expect(q('fetch-blocked')).toBeNull();
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
