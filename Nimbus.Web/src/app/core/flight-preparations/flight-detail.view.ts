import type { FlightDetailDto, FlightImageDto } from '../api-client';
import type { FlightPreparationSectionId } from './flight-preparation.model';

/** What an empty value reads as. */
export const NONE = '—';
const NOT_FILLED_IN = 'not filled in';

/** `ok` green, `warn` amber, `dim` quiet secondary text: the mockup's value colours. */
export type Tone = 'ok' | 'warn' | 'dim';

export interface DetailRow {
  label: string;
  value: string;
  tone?: Tone;
  /** Long free text keeps its line breaks. */
  multiline?: boolean;
  testId: string;
}

/** One checklist line: a tick when everything on it is done, an alert when not. */
export interface DetailCheck {
  text: string;
  done: boolean;
  testId: string;
}

export interface DetailCell {
  text: string;
  right?: boolean;
  /** Small marks after the text (passengers who need help, children, transport). */
  marks?: { icon: string; title: string }[];
}

export interface DetailTable {
  columns: { label: string; right?: boolean }[];
  rows: DetailCell[][];
  testId: string;
}

/** One heading of the document, in the mockup's order. */
export interface DetailGroup {
  id: FlightPreparationSectionId;
  heading: string;
  /** Quiet note after the heading, e.g. the briefing's version. */
  headingNote?: string;
  rows: DetailRow[];
  checks: DetailCheck[];
  tables: DetailTable[];
  /** Rows shown after the tables (a margin under the passenger list). */
  afterRows: DetailRow[];
  /** Pre-written prose shown as a note. */
  note?: { text: string; dim: boolean; testId: string };
  /** Rich text the pilot wrote (already sanitised by the API; the view sanitises again). */
  html: string | null;
  images: FlightImageDto[];
}

export interface StatusCell {
  label: string;
  value: string;
  unit?: string;
  tone?: 'ok' | 'warn' | 'bad';
  testId: string;
}

export interface DetailHeader {
  title: string;
  /** "Fri 8 Aug 2026 · 18:45 · OO-BLK Cameron Z-90 · N. Thys" */
  meta: string;
  roleBadge: { kind: 'shared' | 'admin'; icon: string; text: string } | null;
  readonlyNote: string;
  strip: StatusCell[];
}

type Num = number | string | null | undefined;

const present = (v: Num) => v !== null && v !== undefined && v !== '';
const nonZero = (v: Num) => present(v) && Number(v) !== 0;
const n = (v: Num) => (present(v) ? String(Number(v)) : NONE);
const text = (v: string | null | undefined) => (v && v.trim() !== '' ? v.trim() : '');
const hhmm = (v: string | null | undefined) => (v ? v.slice(0, 5) : NONE);
const yesNo = (v: boolean | undefined) => (v ? 'yes' : 'no');
const signed = (v: number) => `${v > 0 ? '+' : v < 0 ? '−' : ''}${Math.abs(v)}`;

/** "2026-08-08" -> "Fri 8 Aug 2026", from the calendar date alone (no time-zone shifting). */
function longDate(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number);
  if (!y || !m || !d) return iso;
  const parts = new Intl.DateTimeFormat('en-GB', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' }).formatToParts(new Date(y, m - 1, d));
  const get = (type: string) => parts.find((p) => p.type === type)?.value ?? '';
  return `${get('weekday')} ${get('day')} ${get('month')} ${get('year')}`;
}

const row = (testId: string, label: string, value: string, extra: Partial<DetailRow> = {}): DetailRow => ({ testId, label, value, ...extra });

/** The title, the one-line summary, who is looking at it and the status strip (total, lift, margin, Go/No-Go). */
export function buildDetailHeader(flight: FlightDetailDto): DetailHeader {
  const d = flight.details;
  const a = flight.assessment;
  const balloon = [flight.balloonRegistration, flight.balloonType].filter(Boolean).join(' ');
  const meta = [longDate(d.flightDate), hhmm(d.flightTime), balloon, flight.pilotName].filter((p) => p && p !== NONE).join(' · ');

  const roleBadge =
    flight.viewerRole === 'Shared'
      ? { kind: 'shared' as const, icon: 'ti-users-group', text: `shared by ${flight.ownerName ?? 'the owner'}` }
      : flight.viewerRole === 'Admin'
        ? { kind: 'admin' as const, icon: 'ti-shield-check', text: 'admin view' }
        : null;
  const readonlyNote =
    flight.viewerRole === 'Shared' ? 'read-only · no edit rights' : flight.viewerRole === 'Admin' ? 'admin · all flights visible' : 'own flight';

  const liftEntered = nonZero(d.totalLiftKg);
  const strip: StatusCell[] = [
    { label: 'TOTAL', value: n(a.totalWeightKg), unit: 'kg', testId: 'strip-total' },
    { label: 'LIFT', value: liftEntered ? n(d.totalLiftKg) : NONE, unit: liftEntered ? 'kg' : undefined, testId: 'strip-lift' },
    {
      label: 'MARGIN',
      value: a.liftSufficient === null ? NONE : signed(Number(a.marginKg)),
      unit: a.liftSufficient === null ? undefined : 'kg',
      tone: a.liftSufficient === null ? undefined : a.liftSufficient ? 'ok' : 'bad',
      testId: 'strip-margin',
    },
  ];
  return { title: d.title ?? '', meta, roleBadge, readonlyNote, strip };
}

/** Safety checks are three-state (Yes, No, N/A); a "No" is what is still to do. */
function safetyCheck(d: FlightDetailDto['details']): DetailCheck {
  const items: [string, number | undefined][] = [
    ['first aid kit & extinguisher', d.firstAidAndExtinguisher],
    ['passenger list filled', d.passengerListFilled],
    ['flight plan filed', d.flightPlanFiled],
  ];
  const done = items.filter(([, a]) => (a ?? 0) !== 0).map(([name, a]) => (a === 2 ? `${name} (N/A)` : name));
  const todo = items.filter(([, a]) => (a ?? 0) === 0).map(([name]) => name);
  const textLine = [done.join(', '), todo.length ? `not done: ${todo.join(', ')}` : ''].filter(Boolean).join(' — ');
  return { text: textLine, done: todo.length === 0, testId: 'check-safety' };
}

function technicalCheck(d: FlightDetailDto['details']): DetailCheck {
  const items: [string, boolean | undefined][] = [
    ['burner', d.burnerTested],
    ['gas cylinders', d.gasCylindersChecked],
    ['balloon visual', d.balloonVisualInspectionDone],
    ['moorings', d.mooringsChecked],
    ['instruments', d.instrumentsChecked],
  ];
  const done = items.filter(([, v]) => v).map(([name]) => name);
  const todo = items.filter(([, v]) => !v).map(([name]) => name);
  const textLine = [done.join(', '), `${done.length}/${items.length}`, todo.length ? `not done: ${todo.join(', ')}` : ''].filter(Boolean).join(' — ');
  return { text: textLine, done: todo.length === 0, testId: 'check-technical' };
}

function liftRow(flight: FlightDetailDto): DetailRow {
  const a = flight.assessment;
  if (a.liftSufficient === null) return row('lift-margin', 'Margin', 'total lift not entered', { tone: 'dim' });
  const margin = Number(a.marginKg);
  return a.liftSufficient
    ? row('lift-margin', 'Margin', `${signed(margin)} kg — sufficient`, { tone: 'ok' })
    : row('lift-margin', 'Margin', `${signed(margin)} kg — overloaded`, { tone: 'warn' });
}

/**
 * The document under the status strip: the dossier's sections in the form's order, laid out as in
 * the flight-detail mockup (grouped key/value rows, the checklists as tick lines, the passenger
 * list as a small table, the briefing in a box). Pure, so what the page shows can be tested
 * without rendering it. The assessment numbers come from the API's single assessment and are only
 * formatted here, never recomputed. Optional free text that is empty is left out; things a pilot
 * is expected to have filled in say "not filled in".
 */
export function buildDetailGroups(flight: FlightDetailDto): DetailGroup[] {
  const d = flight.details;
  const imagesFor = (id: FlightPreparationSectionId) => flight.images.filter((i) => i.section === id);
  const location = flight.launchLocationName ? `${flight.launchLocationName}${flight.launchLocationIcaoCode ? ` (${flight.launchLocationIcaoCode})` : ''}` : NONE;
  const optionalText = (testId: string, label: string, value: string | null | undefined, extra: Partial<DetailRow> = {}) =>
    text(value) ? [row(testId, label, text(value), { tone: 'dim', ...extra })] : [];

  const group = (id: FlightPreparationSectionId, heading: string, content: Partial<DetailGroup>): DetailGroup => ({
    id, heading, rows: [], checks: [], tables: [], afterRows: [], html: null, images: imagesFor(id), ...content,
  });

  const fuelFilled = nonZero(d.ofpFuelAvailableMinutes) || nonZero(d.ofpFuelRequiredMinutes);
  const cruise = (d.cruiseAltitudesFtCsv ?? '').split(',').map((p) => p.trim()).filter(Boolean);
  const passengers = d.passengers ?? [];
  const windLevels = d.windLevels ?? [];
  const briefingNote = text(d.paxBriefingVersion) ? `· ${d.paxBriefingIsCustom ? 'customized' : 'standard'} ${text(d.paxBriefingVersion)}` : d.paxBriefingIsCustom ? '· customized' : undefined;

  return [
    group('General', '01 · general', {
      rows: [
        row('launch-location', 'Location', location),
        row('sun', 'Sunrise / sunset', `${hhmm(d.sunriseTime)} / ${hhmm(d.sunsetTime)}`),
        row('field-owner-notified', 'Field owner notified', yesNo(d.fieldOwnerNotified), { tone: d.fieldOwnerNotified ? 'ok' : 'warn' }),
      ],
    }),
    group('Meteo', '02 · meteo', {
      rows: [
        row('wind', 'Wind', present(d.windSpeedKt) ? `${n(d.windSpeedKt)} kt / ${n(d.windDirectionDeg)}°` : NONE),
        row('visibility-cape', 'Visibility / CAPE', `${present(d.visibilityKm) ? `${n(d.visibilityKm)} km` : NONE} / ${present(d.capeJPerKg) ? `${n(d.capeJPerKg)} J/kg` : NONE}`),
        row('temp-dew-qnh', 'Temp / dew point / QNH', `${present(d.temperatureC) ? `${n(d.temperatureC)}°C` : NONE} / ${present(d.dewPointC) ? `${n(d.dewPointC)}°C` : NONE} / ${present(d.qnhHpa) ? `${n(d.qnhHpa)} hPa` : NONE}`),
        row('precipitation', 'Precipitation', text(d.precipitation) || NONE),
        row('metar', 'METAR', text(d.metar) || NONE, { tone: 'dim', multiline: true }),
        row('taf', 'TAF', text(d.taf) || NONE, { tone: 'dim', multiline: true }),
      ],
      tables: windLevels.length
        ? [{
            columns: [{ label: 'ALTITUDE' }, { label: 'DIRECTION', right: true }, { label: 'KT', right: true }, { label: '°C', right: true }],
            rows: windLevels.map((w) => [{ text: `${n(w.altitudeM)} m` }, { text: `${n(w.directionDeg)}°`, right: true }, { text: n(w.speedKt), right: true }, { text: n(w.temperatureC), right: true }]),
            testId: 'wind-levels',
          }]
        : [],
    }),
    group('Airspace', '03 · airspace & NOTAMs', {
      rows: [
        row('notams-checked', 'NOTAMs checked', d.notamsChecked ? 'YES' : 'NO', { tone: d.notamsChecked ? 'ok' : 'warn' }),
        ...optionalText('airspace-structure', 'Airspace structure', d.airspaceStructureNotes, { multiline: true }),
        ...optionalText('airspace-restrictions', 'Restrictions', d.airspaceRestrictions, { multiline: true }),
        ...optionalText('obstacles', 'Obstacles', d.obstacles, { multiline: true }),
      ],
    }),
    group('Checklists', '04 · checklists', { checks: [safetyCheck(d), technicalCheck(d)] }),
    group('LoadCalculation', '05 · load calculation', {
      rows: [
        row('balloon-weights', 'Balloon weights', `envelope ${n(d.envelopeWeightKg)} · basket ${n(d.basketWeightKg)} · burner ${n(d.burnerWeightKg)} · cylinders ${n(d.cylindersWeightKg)} kg`),
        row('max-altitude-lift', 'Max altitude / lift units', `${present(d.maxAltitudeFt) ? `${n(d.maxAltitudeFt)} ft` : NONE} / ${n(d.liftUnits)}`),
      ],
      tables: passengers.length
        ? [{
            columns: [{ label: 'PASSENGER' }, { label: 'KG', right: true }],
            rows: passengers.map((p) => [
              {
                text: p.name,
                marks: [
                  ...(p.needsAssistance ? [{ icon: 'ti-first-aid-kit', title: 'Needs assistance' }] : []),
                  ...(p.isChild ? [{ icon: 'ti-baby-carriage', title: 'Child' }] : []),
                  ...(p.needsTransport ? [{ icon: 'ti-car', title: 'Needs transport' }] : []),
                ],
              },
              { text: n(p.weightKg), right: true },
            ]),
            testId: 'passengers',
          }]
        : [],
      afterRows: [liftRow(flight), ...optionalText('load-remarks', 'Remarks', d.loadRemarks, { multiline: true })],
    }),
    group('Route', '06 · route & trajectory', {
      rows: [
        row('route-description', 'Route', text(d.routeDescription) || NONE, { tone: 'dim', multiline: true }),
        row('cruise-altitudes', 'Cruise altitudes', cruise.length ? `${cruise.join(' / ')} ft` : NONE),
        ...(nonZero(d.climbRateMetersPerSecond) ? [row('climb-rate', 'Climb rate', `${n(d.climbRateMetersPerSecond)} m/s`)] : []),
        ...(nonZero(d.descentRateMetersPerSecond) ? [row('descent-rate', 'Descent rate', `${n(d.descentRateMetersPerSecond)} m/s`)] : []),
      ],
    }),
    group('Bulletin', '07 · balloon bulletin', { note: { text: text(d.bulletinText) || NONE, dim: !text(d.bulletinText), testId: 'bulletin' } }),
    group('Ofp', '08 · OFP', {
      rows: [
        row('ofp-operator', 'Operator / PIC weight', `${text(d.ofpOperatorName) || NONE} / ${nonZero(d.ofpPicWeightKg) ? `${n(d.ofpPicWeightKg)} kg` : NONE}`),
        row('ofp-fuel', 'Fuel available / required', fuelFilled ? `${n(d.ofpFuelAvailableMinutes)} / ${n(d.ofpFuelRequiredMinutes)} min` : NOT_FILLED_IN, { tone: fuelFilled ? undefined : 'warn' }),
        row('ofp-landing-location', 'Landing location', text(d.ofpLandingLocation) || NOT_FILLED_IN, { tone: text(d.ofpLandingLocation) ? 'dim' : 'warn' }),
      ],
    }),
    group('PaxBriefing', 'pax briefing', {
      headingNote: briefingNote,
      html: text(d.paxBriefingText) ? d.paxBriefingText! : null,
    }),
  ];
}
