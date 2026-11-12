import { AbstractControl, FormArray, NonNullableFormBuilder, Validators } from '@angular/forms';

import {
  ChecklistAnswer,
  FLIGHT_PREPARATION_SECTION_IDS,
  type FlightPreparationSectionId,
  sectionFormKey,
  type SectionCompletionStatus,
} from './flight-preparation.model';

/**
 * Builds the single reactive form that drives the whole nine-section dossier (issue #32): one
 * `FormGroup` per section, nested under a parent group so the dossier's overall validity/dirty
 * state is always known from one place, while each section component only ever sees its own
 * slice (`form.controls.general`, `form.controls.meteo`, ...).
 *
 * Field names mirror the flat columns documented in `docs/flight-preparation.md` 1:1, so mapping
 * `getRawValue()` onto the real `FlightPreparation` request DTO (once it exists) is a reshape, not
 * a redesign.
 */
export function createFlightPreparationForm(fb: NonNullableFormBuilder) {
  return fb.group({
    general: fb.group({
      flightDate: ['', Validators.required],
      flightTime: ['', Validators.required],
      balloonId: ['', Validators.required],
      pilotId: ['', Validators.required],
      launchLocationId: ['', Validators.required],
      /** Optional: the landing site is often only known after the flight. */
      landingLocationId: [''],
      sunriseTime: [''],
      sunsetTime: [''],
      fieldOwnerNotified: [false],
    }),
    meteo: fb.group({
      windSpeedKt: [0, [Validators.required, Validators.min(0)]],
      windDirectionDeg: [0, [Validators.required, Validators.min(0), Validators.max(360)]],
      visibilityKm: [0, [Validators.min(0)]],
      capeJPerKg: [0],
      temperatureC: [0],
      dewPointC: [0],
      qnhHpa: [0],
      precipitation: [''],
      metar: [''],
      taf: [''],
      windLevels: fb.array([createWindLevelGroup(fb)]),
    }),
    airspace: fb.group({
      notamsChecked: [false, Validators.requiredTrue],
      airspaceStructureNotes: [''],
      airspaceRestrictions: [''],
      obstacles: [''],
    }),
    checklists: fb.group({
      // Safety checks: three-state, default No (unchecked) — never pre-ticked.
      firstAidAndExtinguisher: [ChecklistAnswer.No as ChecklistAnswer],
      passengerListFilled: [ChecklistAnswer.No as ChecklistAnswer],
      flightPlanFiled: [ChecklistAnswer.No as ChecklistAnswer],
      // Technical checks: always applicable, so plain checkboxes defaulting to unchecked.
      burnerTested: [false],
      gasCylindersChecked: [false],
      balloonVisualInspectionDone: [false],
      mooringsChecked: [false],
      instrumentsChecked: [false],
    }),
    loadCalculation: fb.group({
      envelopeWeightKg: [0, [Validators.required, Validators.min(0)]],
      // Snapshotted from the balloon's reference weights when it is picked on a new flight.
      basketWeightKg: [0, [Validators.min(0)]],
      burnerWeightKg: [0, [Validators.min(0)]],
      cylindersWeightKg: [0, [Validators.min(0)]],
      maxAltitudeFt: [0, [Validators.required, Validators.min(0)]],
      /** Number of lift units and the resulting total lift (kg) as entered by the pilot. The total
       *  weight and the lift-vs-weight verdict are derived (see `calculateLoad`), never stored. */
      liftUnits: [0, [Validators.min(0)]],
      totalLiftKg: [0, [Validators.min(0)]],
      loadRemarks: [''],
      passengers: fb.array<ReturnType<typeof createPassengerGroup>>([]),
    }),
    route: fb.group({
      routeDescription: [''],
      climbRateMetersPerSecond: [0, [Validators.min(0)]],
      descentRateMetersPerSecond: [0, [Validators.min(0)]],
      cruiseAltitudesFtCsv: [''],
    }),
    bulletin: fb.group({
      bulletinText: [''],
    }),
    ofp: fb.group({
      ofpOperatorName: ['', Validators.required],
      ofpPicWeightKg: [0, [Validators.min(0)]],
      ofpFuelAvailableMinutes: [0, [Validators.min(0)]],
      ofpFuelRequiredMinutes: [0, [Validators.min(0)]],
      ofpLandingLocation: [''],
    }),
    paxBriefing: fb.group({
      paxBriefingText: [''],
      paxBriefingVersion: [''],
      paxBriefingIsCustom: [false],
    }),
  });
}

/** Morning and evening are the usual balloon launch windows (local time). */
const DEFAULT_SLOT_HOURS = [7, 19] as const;

/**
 * The next sensible flight slot: the first launch window (07:00 or 19:00 local) at least an hour
 * from `now`, rolling over to tomorrow morning. Used to prefill a new flight's date and time.
 */
export function nextFlightSlot(now: Date = new Date()): { flightDate: string; flightTime: string } {
  const earliest = now.getTime() + 60 * 60 * 1000;
  for (let dayOffset = 0; dayOffset < 2; dayOffset++) {
    for (const hour of DEFAULT_SLOT_HOURS) {
      const candidate = new Date(now.getFullYear(), now.getMonth(), now.getDate() + dayOffset, hour, 0, 0, 0);
      if (candidate.getTime() >= earliest) {
        const pad = (n: number) => String(n).padStart(2, '0');
        return {
          flightDate: `${candidate.getFullYear()}-${pad(candidate.getMonth() + 1)}-${pad(candidate.getDate())}`,
          flightTime: `${pad(hour)}:00`,
        };
      }
    }
  }
  throw new Error('unreachable');
}

export function createWindLevelGroup(fb: NonNullableFormBuilder) {
  return fb.group({
    altitudeM: [0, [Validators.required, Validators.min(0)]],
    directionDeg: [0, [Validators.required, Validators.min(0), Validators.max(360)]],
    speedKt: [0, [Validators.required, Validators.min(0)]],
    temperatureC: [0],
  });
}

export function createPassengerGroup(fb: NonNullableFormBuilder) {
  return fb.group({
    name: ['', Validators.required],
    weightKg: [0, [Validators.required, Validators.min(1)]],
    needsAssistance: [false],
    isChild: [false],
    needsTransport: [false],
  });
}

/** The three-state safety checks (section 04, first group). */
export const SAFETY_CHECK_KEYS = ['firstAidAndExtinguisher', 'passengerListFilled', 'flightPlanFiled'] as const;

/** The five technical checks (section 04, second group). */
export const TECHNICAL_CHECK_KEYS = [
  'burnerTested',
  'gasCylindersChecked',
  'balloonVisualInspectionDone',
  'mooringsChecked',
  'instrumentsChecked',
] as const;

/** How many of the eight checks are done: a safety answer counts once it is Yes or N/A. */
export function checklistProgress(value: FlightPreparationFormValue['checklists']): { done: number; total: number } {
  const safety = SAFETY_CHECK_KEYS.filter((k) => value[k] !== ChecklistAnswer.No).length;
  const technical = TECHNICAL_CHECK_KEYS.filter((k) => value[k]).length;
  return { done: safety + technical, total: SAFETY_CHECK_KEYS.length + TECHNICAL_CHECK_KEYS.length };
}

export type FlightPreparationForm = ReturnType<typeof createFlightPreparationForm>;
export type FlightPreparationFormValue = ReturnType<FlightPreparationForm['getRawValue']>;
export type GeneralFormGroup = FlightPreparationForm['controls']['general'];
export type MeteoFormGroup = FlightPreparationForm['controls']['meteo'];
export type AirspaceFormGroup = FlightPreparationForm['controls']['airspace'];
export type ChecklistsFormGroup = FlightPreparationForm['controls']['checklists'];
export type LoadCalculationFormGroup = FlightPreparationForm['controls']['loadCalculation'];
export type RouteFormGroup = FlightPreparationForm['controls']['route'];
export type BulletinFormGroup = FlightPreparationForm['controls']['bulletin'];
export type OfpFormGroup = FlightPreparationForm['controls']['ofp'];
export type PaxBriefingFormGroup = FlightPreparationForm['controls']['paxBriefing'];

/** Moves a passenger within the list (the array order is the persisted passenger order). */
export function movePassenger(array: FormArray, from: number, to: number): void {
  if (from === to || from < 0 || to < 0 || from >= array.length || to >= array.length) return;
  const control = array.at(from);
  array.removeAt(from, { emitEvent: false });
  array.insert(to, control);
}

/**
 * Rebuilds a freshly-constructed form's dynamic `FormArray`s (wind levels, passengers) to match a loaded draft's shape, then patches in every value. Needed because
 * `FormGroup.reset()` only resets *existing* controls — it can't grow/shrink a `FormArray` to a
 * different saved flight's number of rows, which is why switching between two drafts rebuilds the
 * whole form (see `FlightPreparationDraftStore` / the form page) rather than reusing one instance.
 */
export function hydrateFlightPreparationForm(
  form: FlightPreparationForm,
  fb: NonNullableFormBuilder,
  value: FlightPreparationFormValue,
): void {
  resizeArray(form.controls.meteo.controls.windLevels, value.meteo.windLevels.length, () => createWindLevelGroup(fb));
  resizeArray(form.controls.loadCalculation.controls.passengers, value.loadCalculation.passengers.length, () =>
    createPassengerGroup(fb),
  );
  form.patchValue(value);
}

function resizeArray<T extends AbstractControl>(array: FormArray<T>, targetLength: number, create: () => T): void {
  while (array.length < targetLength) array.push(create());
  while (array.length > targetLength) array.removeAt(array.length - 1);
}

/**
 * Which sections differ between two loaded dossier values — used to tell a restored
 * autosave-after-refresh apart from the last explicitly saved version (issue #32 mockup frame 3A:
 * "● FROM DRAFT"), and to summarize what changed in the unsaved-changes dialog (frame 2B).
 */
export function diffingSectionIds(
  a: FlightPreparationFormValue,
  b: FlightPreparationFormValue,
): FlightPreparationSectionId[] {
  return FLIGHT_PREPARATION_SECTION_IDS.filter((id) => {
    const key = sectionFormKey(id) as keyof FlightPreparationFormValue;
    return JSON.stringify(a[key]) !== JSON.stringify(b[key]);
  });
}

/**
 * A section's completion dot (issue #32 / mockup "Flight Prep Form Mockups" frames 1A/2A):
 * `'complete'` once the section validates — regardless of whether it's been touched, so a
 * preloaded flight with already-valid sections reads as done immediately; `'invalid'` once an
 * invalid section has been visited (blurred/touched) or a save was attempted; `'partial'` once an
 * invalid section already has *some* values filled in but hasn't been visited/submitted yet (e.g.
 * profile defaults or a restored draft); `'pending'` while fully empty and untouched.
 */
export function sectionCompletionStatus(group: AbstractControl): SectionCompletionStatus {
  if (group.valid) return 'complete';
  if (group.touched) return 'invalid';
  return hasAnyValue(group.getRawValue()) ? 'partial' : 'pending';
}

/** Counts invalid *leaf* controls under a group/array (a nested group being invalid because one
 *  of its children is invalid is not double-counted) — drives the save-error banner's per-section
 *  error counts (issue #32 mockup frame 2A). */
export function countInvalidLeaves(control: AbstractControl): number {
  const children = (control as FormArray | { controls?: unknown }).controls;
  if (children && typeof children === 'object') {
    return Object.values(children as Record<string, AbstractControl>).reduce(
      (sum, child) => sum + countInvalidLeaves(child),
      0,
    );
  }
  return control.invalid ? 1 : 0;
}

/** True if any leaf value in a (possibly nested) raw form value is non-empty/non-default —
 *  used to tell a freshly-opened, still-empty section apart from one that already has some
 *  values filled in but isn't valid yet (`'pending'` vs `'partial'`). */
export function hasAnyValue(value: unknown): boolean {
  if (value === null || value === undefined) return false;
  if (typeof value === 'string') return value.trim() !== '';
  if (typeof value === 'number') return value !== 0;
  if (typeof value === 'boolean') return value === true;
  if (Array.isArray(value)) return value.some(hasAnyValue);
  if (typeof value === 'object') return Object.values(value).some(hasAnyValue);
  return false;
}

