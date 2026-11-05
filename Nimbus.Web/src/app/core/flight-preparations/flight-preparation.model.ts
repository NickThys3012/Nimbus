/**
 * The nine pre-flight dossier sections, in the same order and with the same ids as the backend's
 * `FlightPreparationSection` enum (see `docs/flight-preparation.md`) — kept 1:1 so wiring the real
 * save/load endpoints (issues #40/#51-style) later is a seam, not a rename.
 */
export const FLIGHT_PREPARATION_SECTION_IDS = [
  'General',
  'Meteo',
  'Airspace',
  'Checklists',
  'LoadCalculation',
  'Route',
  'Bulletin',
  'Ofp',
  'PaxBriefing',
] as const;

export type FlightPreparationSectionId = (typeof FLIGHT_PREPARATION_SECTION_IDS)[number];

export interface FlightPreparationSectionMeta {
  id: FlightPreparationSectionId;
  order: number;
  title: string;
}

export const FLIGHT_PREPARATION_SECTIONS: readonly FlightPreparationSectionMeta[] = [
  { id: 'General', order: 1, title: 'General' },
  { id: 'Meteo', order: 2, title: 'Meteo' },
  { id: 'Airspace', order: 3, title: 'Airspace & NOTAMs' },
  { id: 'Checklists', order: 4, title: 'Checklists' },
  { id: 'LoadCalculation', order: 5, title: 'Load calculation' },
  { id: 'Route', order: 6, title: 'Route & trajectory' },
  { id: 'Bulletin', order: 7, title: 'Balloon bulletin' },
  { id: 'Ofp', order: 8, title: 'OFP' },
  { id: 'PaxBriefing', order: 9, title: 'Passenger briefing' },
];

/** The id a not-yet-saved flight preparation uses in the route until it gets a real id. */
export const NEW_FLIGHT_PREPARATION_ID = 'new';

/** A section header's completion dot: `'complete'` (green) once its required fields validate,
 *  `'invalid'` (red) once an invalid section has been visited/submitted, `'partial'` (amber) once
 *  it has some values but is still missing required ones, and `'pending'` (grey) while fully
 *  untouched/empty. */
export type SectionCompletionStatus = 'complete' | 'invalid' | 'partial' | 'pending';

/** `'LoadCalculation'` -> `'loadCalculation'`: the parent form's control key for a section id. */
export function sectionFormKey(id: FlightPreparationSectionId): string {
  return id.charAt(0).toLowerCase() + id.slice(1);
}
