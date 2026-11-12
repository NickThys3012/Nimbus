import type { FlightPreparationDto, FlightPreparationRequestDto } from '../api-client';
import { ChecklistAnswer } from './flight-preparation.model';
import type { FlightPreparationFormValue } from './flight-preparation-form';

/** The backend serialises `ChecklistAnswer` as its number; the form works with the names. */
const ANSWER_TO_NUMBER: Record<ChecklistAnswer, number> = {
  [ChecklistAnswer.No]: 0,
  [ChecklistAnswer.Yes]: 1,
  [ChecklistAnswer.NotApplicable]: 2,
};
const NUMBER_TO_ANSWER: Record<number, ChecklistAnswer> = {
  0: ChecklistAnswer.No,
  1: ChecklistAnswer.Yes,
  2: ChecklistAnswer.NotApplicable,
};

const toAnswer = (n: number | undefined): ChecklistAnswer => NUMBER_TO_ANSWER[n ?? 0] ?? ChecklistAnswer.No;

/** A blank optional time is "no value", never an empty string the API cannot parse. */
const orNull = (s: string): string | null => (s.trim() === '' ? null : s);
/** `07:00:00` from the API -> the `07:00` a time input holds. */
const hhmm = (s: string | null | undefined): string => (s ?? '').slice(0, 5);
const num = (n: number | string | null | undefined): number => (n === null || n === undefined ? 0 : Number(n));

/** The list label for a flight: where and when, e.g. "Weelde · 2026-08-08". */
export function flightTitle(locationName: string | null | undefined, flightDate: string): string {
  return locationName ? `${locationName} · ${flightDate}` : `Flight ${flightDate}`;
}

/**
 * Reshapes the form's value into the API request. The landing-site pick has no column and lives
 * only in the local draft, so it is left out.
 */
export function toRequestDto(value: FlightPreparationFormValue, title: string): FlightPreparationRequestDto {
  const { general, meteo, airspace, checklists, loadCalculation: load, route, bulletin, ofp, paxBriefing } = value;
  return {
    title,
    balloonId: general.balloonId,
    pilotId: general.pilotId,
    launchLocationId: general.launchLocationId,
    flightDate: general.flightDate,
    flightTime: general.flightTime,
    sunriseTime: orNull(general.sunriseTime),
    sunsetTime: orNull(general.sunsetTime),
    fieldOwnerNotified: general.fieldOwnerNotified,
    windSpeedKt: meteo.windSpeedKt,
    windDirectionDeg: meteo.windDirectionDeg,
    visibilityKm: meteo.visibilityKm,
    capeJPerKg: meteo.capeJPerKg,
    temperatureC: meteo.temperatureC,
    dewPointC: meteo.dewPointC,
    qnhHpa: meteo.qnhHpa,
    // Autosaved drafts from before the field existed have no value for it.
    precipitation: orNull(meteo.precipitation ?? ''),
    metar: meteo.metar,
    taf: meteo.taf,
    windLevels: meteo.windLevels.map((w) => ({ ...w })),
    notamsChecked: airspace.notamsChecked,
    airspaceStructureNotes: airspace.airspaceStructureNotes,
    airspaceRestrictions: airspace.airspaceRestrictions,
    obstacles: airspace.obstacles,
    firstAidAndExtinguisher: ANSWER_TO_NUMBER[checklists.firstAidAndExtinguisher],
    passengerListFilled: ANSWER_TO_NUMBER[checklists.passengerListFilled],
    flightPlanFiled: ANSWER_TO_NUMBER[checklists.flightPlanFiled],
    burnerTested: checklists.burnerTested,
    gasCylindersChecked: checklists.gasCylindersChecked,
    balloonVisualInspectionDone: checklists.balloonVisualInspectionDone,
    mooringsChecked: checklists.mooringsChecked,
    instrumentsChecked: checklists.instrumentsChecked,
    envelopeWeightKg: load.envelopeWeightKg,
    basketWeightKg: load.basketWeightKg,
    burnerWeightKg: load.burnerWeightKg,
    cylindersWeightKg: load.cylindersWeightKg,
    maxAltitudeFt: load.maxAltitudeFt,
    liftUnits: load.liftUnits,
    totalLiftKg: load.totalLiftKg,
    loadRemarks: load.loadRemarks,
    passengers: load.passengers.map((p) => ({ ...p })),
    routeDescription: route.routeDescription,
    climbRateMetersPerSecond: route.climbRateMetersPerSecond,
    descentRateMetersPerSecond: route.descentRateMetersPerSecond,
    cruiseAltitudesFtCsv: route.cruiseAltitudesFtCsv,
    bulletinText: bulletin.bulletinText,
    ofpOperatorName: ofp.ofpOperatorName,
    ofpPicWeightKg: ofp.ofpPicWeightKg,
    ofpFuelAvailableMinutes: ofp.ofpFuelAvailableMinutes,
    ofpFuelRequiredMinutes: ofp.ofpFuelRequiredMinutes,
    ofpLandingLocation: ofp.ofpLandingLocation,
    paxBriefingText: paxBriefing.paxBriefingText,
    paxBriefingVersion: paxBriefing.paxBriefingVersion,
    paxBriefingIsCustom: paxBriefing.paxBriefingIsCustom,
  };
}

/** Reshapes a stored flight into the form's value. */
export function fromDto(dto: FlightPreparationDto): FlightPreparationFormValue {
  const d = dto.details;
  return {
    general: {
      flightDate: d.flightDate,
      flightTime: hhmm(d.flightTime),
      balloonId: d.balloonId,
      pilotId: d.pilotId,
      launchLocationId: d.launchLocationId,
      landingLocationId: '',
      sunriseTime: hhmm(d.sunriseTime),
      sunsetTime: hhmm(d.sunsetTime),
      fieldOwnerNotified: d.fieldOwnerNotified ?? false,
    },
    meteo: {
      windSpeedKt: num(d.windSpeedKt),
      windDirectionDeg: num(d.windDirectionDeg),
      visibilityKm: num(d.visibilityKm),
      capeJPerKg: num(d.capeJPerKg),
      temperatureC: num(d.temperatureC),
      dewPointC: num(d.dewPointC),
      qnhHpa: num(d.qnhHpa),
      precipitation: d.precipitation ?? '',
      metar: d.metar ?? '',
      taf: d.taf ?? '',
      windLevels: (d.windLevels ?? []).map((w) => ({
        altitudeM: num(w.altitudeM),
        directionDeg: num(w.directionDeg),
        speedKt: num(w.speedKt),
        temperatureC: num(w.temperatureC),
      })),
    },
    airspace: {
      notamsChecked: d.notamsChecked ?? false,
      airspaceStructureNotes: d.airspaceStructureNotes ?? '',
      airspaceRestrictions: d.airspaceRestrictions ?? '',
      obstacles: d.obstacles ?? '',
    },
    checklists: {
      firstAidAndExtinguisher: toAnswer(d.firstAidAndExtinguisher),
      passengerListFilled: toAnswer(d.passengerListFilled),
      flightPlanFiled: toAnswer(d.flightPlanFiled),
      burnerTested: d.burnerTested ?? false,
      gasCylindersChecked: d.gasCylindersChecked ?? false,
      balloonVisualInspectionDone: d.balloonVisualInspectionDone ?? false,
      mooringsChecked: d.mooringsChecked ?? false,
      instrumentsChecked: d.instrumentsChecked ?? false,
    },
    loadCalculation: {
      envelopeWeightKg: num(d.envelopeWeightKg),
      basketWeightKg: num(d.basketWeightKg),
      burnerWeightKg: num(d.burnerWeightKg),
      cylindersWeightKg: num(d.cylindersWeightKg),
      maxAltitudeFt: num(d.maxAltitudeFt),
      liftUnits: num(d.liftUnits),
      totalLiftKg: num(d.totalLiftKg),
      loadRemarks: d.loadRemarks ?? '',
      passengers: (d.passengers ?? []).map((p) => ({
        name: p.name,
        weightKg: num(p.weightKg),
        needsAssistance: p.needsAssistance,
        isChild: p.isChild,
        needsTransport: p.needsTransport,
      })),
    },
    route: {
      routeDescription: d.routeDescription ?? '',
      climbRateMetersPerSecond: num(d.climbRateMetersPerSecond),
      descentRateMetersPerSecond: num(d.descentRateMetersPerSecond),
      cruiseAltitudesFtCsv: d.cruiseAltitudesFtCsv ?? '',
    },
    bulletin: { bulletinText: d.bulletinText ?? '' },
    ofp: {
      ofpOperatorName: d.ofpOperatorName ?? '',
      ofpPicWeightKg: num(d.ofpPicWeightKg),
      ofpFuelAvailableMinutes: num(d.ofpFuelAvailableMinutes),
      ofpFuelRequiredMinutes: num(d.ofpFuelRequiredMinutes),
      ofpLandingLocation: d.ofpLandingLocation ?? '',
    },
    paxBriefing: {
      paxBriefingText: d.paxBriefingText ?? '',
      paxBriefingVersion: d.paxBriefingVersion ?? '',
      paxBriefingIsCustom: d.paxBriefingIsCustom ?? false,
    },
  };
}
