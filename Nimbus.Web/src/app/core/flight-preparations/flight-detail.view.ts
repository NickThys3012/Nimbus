import type { FlightDetailDto, FlightImageDto } from '../api-client';
import { FLIGHT_PREPARATION_SECTIONS, type FlightPreparationSectionId } from './flight-preparation.model';

/** What an empty value reads as. */
export const NONE = '—';

export interface DetailRow {
  label: string;
  value: string;
  /** Long free text: keeps its line breaks. */
  multiline?: boolean;
  /** Worked out by the API (assessment), not typed in: shown set apart. */
  computed?: boolean;
  testId: string;
}

export interface DetailTable {
  caption: string;
  columns: string[];
  rows: string[][];
  testId: string;
}

export interface DetailSection {
  id: FlightPreparationSectionId;
  order: number;
  title: string;
  rows: DetailRow[];
  /** Pre-formatted blocks (METAR, TAF). */
  blocks: { label: string; value: string; testId: string }[];
  tables: DetailTable[];
  /** Rich text the pilot wrote (already sanitised by the API; the view sanitises again). */
  html: string | null;
  images: FlightImageDto[];
}

type Num = number | string | null | undefined;

const present = (v: Num) => v !== null && v !== undefined && v !== '';
const num = (v: Num, unit = '') => (present(v) ? `${Number(v)}${unit ? ` ${unit}` : ''}` : NONE);
const text = (v: string | null | undefined) => (v && v.trim() !== '' ? v : NONE);
const yesNo = (v: boolean | undefined) => (v ? 'Yes' : 'No');
const hhmm = (v: string | null | undefined) => (v ? v.slice(0, 5) : NONE);
const answer = (v: number | undefined) => ['No', 'Yes', 'N/A'][v ?? 0] ?? 'No';
const signed = (v: number) => `${v > 0 ? '+' : ''}${v} kg`;

const row = (testId: string, label: string, value: string, extra: Partial<DetailRow> = {}): DetailRow => ({ testId, label, value, ...extra });

/** "3000, 4000" -> "3000 ft, 4000 ft". */
function cruiseAltitudes(csv: string | null | undefined): string {
  const parts = (csv ?? '').split(',').map((p) => p.trim()).filter(Boolean);
  return parts.length ? parts.map((p) => `${p} ft`).join(', ') : NONE;
}

function liftVerdict(sufficient: boolean | null): string {
  if (sufficient === null) return 'Total lift not entered';
  return sufficient ? 'Sufficient' : 'Overloaded';
}

/**
 * The read-only dossier: every section of the form, in the form's order, as plain rows, blocks and
 * tables. Pure, so what the page shows can be tested without rendering it. The assessment numbers
 * come from the API's single assessment and are only formatted here, never recomputed.
 */
export function buildDetailSections(flight: FlightDetailDto): DetailSection[] {
  const d = flight.details;
  const a = flight.assessment;
  const imagesFor = (id: FlightPreparationSectionId) => flight.images.filter((i) => i.section === id);
  const location = flight.launchLocationName
    ? `${flight.launchLocationName}${flight.launchLocationIcaoCode ? ` (${flight.launchLocationIcaoCode})` : ''}`
    : NONE;

  const content: Record<FlightPreparationSectionId, Omit<DetailSection, 'id' | 'order' | 'title' | 'images'>> = {
    General: {
      rows: [
        row('flight-date', 'Date', text(d.flightDate)),
        row('flight-time', 'Time', hhmm(d.flightTime)),
        row('sunrise', 'Sunrise', hhmm(d.sunriseTime)),
        row('sunset', 'Sunset', hhmm(d.sunsetTime)),
        row('balloon', 'Balloon', text(flight.balloonRegistration)),
        row('pilot', 'Pilot', text(flight.pilotName)),
        row('launch-location', 'Launch location', location),
        row('field-owner-notified', 'Field owner notified', yesNo(d.fieldOwnerNotified)),
      ],
      blocks: [],
      tables: [],
      html: null,
    },
    Meteo: {
      rows: [
        row('wind', 'Wind', present(d.windSpeedKt) ? `${Number(d.windSpeedKt)} kt · ${Number(d.windDirectionDeg ?? 0)}°` : NONE),
        row('visibility', 'Visibility', num(d.visibilityKm, 'km')),
        row('cape', 'CAPE', num(d.capeJPerKg, 'J/kg')),
        row('temperature', 'Temperature', num(d.temperatureC, '°C')),
        row('dew-point', 'Dew point', num(d.dewPointC, '°C')),
        row('qnh', 'QNH', num(d.qnhHpa, 'hPa')),
      ],
      blocks: [
        { label: 'METAR', value: text(d.metar), testId: 'metar' },
        { label: 'TAF', value: text(d.taf), testId: 'taf' },
      ],
      tables: (d.windLevels ?? []).length
        ? [
            {
              caption: 'Wind by altitude',
              columns: ['Altitude', 'Direction', 'Speed', 'Temperature'],
              rows: (d.windLevels ?? []).map((w) => [`${Number(w.altitudeM)} m`, `${Number(w.directionDeg)}°`, `${Number(w.speedKt)} kt`, `${Number(w.temperatureC)} °C`]),
              testId: 'wind-levels',
            },
          ]
        : [],
      html: null,
    },
    Airspace: {
      rows: [
        row('notams-checked', 'NOTAMs checked', yesNo(d.notamsChecked)),
        row('airspace-structure', 'Airspace structure', text(d.airspaceStructureNotes), { multiline: true }),
        row('airspace-restrictions', 'Restrictions', text(d.airspaceRestrictions), { multiline: true }),
        row('obstacles', 'Obstacles', text(d.obstacles), { multiline: true }),
      ],
      blocks: [],
      tables: [],
      html: null,
    },
    Checklists: {
      rows: [
        row('first-aid', 'First aid kit and extinguisher', answer(d.firstAidAndExtinguisher)),
        row('passenger-list', 'Passenger list filled', answer(d.passengerListFilled)),
        row('flight-plan', 'Flight plan filed', answer(d.flightPlanFiled)),
        row('burner-tested', 'Burner tested', yesNo(d.burnerTested)),
        row('gas-cylinders', 'Gas cylinders checked', yesNo(d.gasCylindersChecked)),
        row('visual-inspection', 'Balloon visual inspection', yesNo(d.balloonVisualInspectionDone)),
        row('moorings', 'Moorings checked', yesNo(d.mooringsChecked)),
        row('instruments', 'Instruments checked', yesNo(d.instrumentsChecked)),
      ],
      blocks: [],
      tables: [],
      html: null,
    },
    LoadCalculation: {
      rows: [
        row('envelope-weight', 'Envelope', num(d.envelopeWeightKg, 'kg')),
        row('basket-weight', 'Basket', num(d.basketWeightKg, 'kg')),
        row('burner-weight', 'Burner', num(d.burnerWeightKg, 'kg')),
        row('cylinders-weight', 'Cylinders', num(d.cylindersWeightKg, 'kg')),
        row('max-altitude', 'Max altitude', num(d.maxAltitudeFt, 'ft')),
        row('lift-units', 'Lift units', num(d.liftUnits)),
        row('total-lift', 'Total lift', num(d.totalLiftKg, 'kg')),
        row('load-remarks', 'Remarks', text(d.loadRemarks), { multiline: true }),
        row('passengers-weight', 'Passengers', `${Number(a.passengersWeightKg)} kg`, { computed: true }),
        row('total-weight', 'Total weight', `${Number(a.totalWeightKg)} kg`, { computed: true }),
        row('lift-margin', 'Lift margin', signed(Number(a.marginKg)), { computed: true }),
        row('lift-verdict', 'Lift', liftVerdict(a.liftSufficient), { computed: true }),
      ],
      blocks: [],
      tables: (d.passengers ?? []).length
        ? [
            {
              caption: 'Passengers',
              columns: ['Name', 'Weight', 'Notes'],
              rows: (d.passengers ?? []).map((p) => [
                p.name,
                `${Number(p.weightKg)} kg`,
                [p.isChild ? 'child' : '', p.needsAssistance ? 'needs assistance' : '', p.needsTransport ? 'needs transport' : ''].filter(Boolean).join(', ') || NONE,
              ]),
              testId: 'passengers',
            },
          ]
        : [],
      html: null,
    },
    Route: {
      rows: [
        row('route-description', 'Route', text(d.routeDescription), { multiline: true }),
        row('climb-rate', 'Climb rate', num(d.climbRateMetersPerSecond, 'm/s')),
        row('descent-rate', 'Descent rate', num(d.descentRateMetersPerSecond, 'm/s')),
        row('cruise-altitudes', 'Cruise altitudes', cruiseAltitudes(d.cruiseAltitudesFtCsv)),
      ],
      blocks: [],
      tables: [],
      html: null,
    },
    Bulletin: {
      rows: [row('bulletin', 'Bulletin', text(d.bulletinText), { multiline: true })],
      blocks: [],
      tables: [],
      html: null,
    },
    Ofp: {
      rows: [
        row('ofp-operator', 'Operator', text(d.ofpOperatorName)),
        row('ofp-pic-weight', 'PIC weight', num(d.ofpPicWeightKg, 'kg')),
        row('ofp-fuel-available', 'Fuel available', num(d.ofpFuelAvailableMinutes, 'min')),
        row('ofp-fuel-required', 'Fuel required', num(d.ofpFuelRequiredMinutes, 'min')),
        row('ofp-landing-location', 'Landing location', text(d.ofpLandingLocation)),
      ],
      blocks: [],
      tables: [],
      html: null,
    },
    PaxBriefing: {
      rows: [
        row('pax-version', 'Version', text(d.paxBriefingVersion)),
        row('pax-custom', 'Customized for this flight', yesNo(d.paxBriefingIsCustom)),
      ],
      blocks: [],
      tables: [],
      html: d.paxBriefingText && d.paxBriefingText.trim() !== '' ? d.paxBriefingText : null,
    },
  };

  return FLIGHT_PREPARATION_SECTIONS.map((s) => ({ id: s.id, order: s.order, title: s.title, images: imagesFor(s.id), ...content[s.id] }));
}
