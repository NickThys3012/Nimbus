/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { ChecklistAnswer } from './ChecklistAnswer';
import type { PassengerDto } from './PassengerDto';
import type { WindLevelDto } from './WindLevelDto';
/**
 * The columns the dossier form owns (sections 1-9). Anything the form does not own (flight outcome, simulation
 * settings, trajectory, KML, shares, images) is never part of this request and survives a save untouched.
 * The owner is never part of a request.
 */
export type FlightPreparationRequestDto = {
    title?: string | null;
    balloonId: string;
    pilotId: string;
    launchLocationId: string;
    flightDate: string;
    flightTime: string;
    sunriseTime?: string | null;
    sunsetTime?: string | null;
    fieldOwnerNotified?: boolean;
    windSpeedKt?: number | string | null;
    windDirectionDeg?: number | null;
    visibilityKm?: number | string | null;
    capeJPerKg?: number | string | null;
    temperatureC?: number | string | null;
    dewPointC?: number | string | null;
    qnhHpa?: number | string | null;
    metar?: string | null;
    taf?: string | null;
    windLevels?: Array<WindLevelDto>;
    notamsChecked?: boolean;
    airspaceStructureNotes?: string | null;
    airspaceRestrictions?: string | null;
    obstacles?: string | null;
    firstAidAndExtinguisher?: ChecklistAnswer;
    passengerListFilled?: ChecklistAnswer;
    flightPlanFiled?: ChecklistAnswer;
    burnerTested?: boolean;
    gasCylindersChecked?: boolean;
    balloonVisualInspectionDone?: boolean;
    mooringsChecked?: boolean;
    instrumentsChecked?: boolean;
    envelopeWeightKg?: number | string | null;
    maxAltitudeFt?: number | null;
    liftUnits?: number | string | null;
    totalLiftKg?: number | string | null;
    loadRemarks?: string | null;
    passengers?: Array<PassengerDto>;
    routeDescription?: string | null;
    climbRateMetersPerSecond?: number | string | null;
    descentRateMetersPerSecond?: number | string | null;
    cruiseAltitudesFtCsv?: string | null;
    bulletinText?: string | null;
    ofpOperatorName?: string | null;
    ofpPicWeightKg?: number | string | null;
    ofpFuelAvailableMinutes?: number | null;
    ofpFuelRequiredMinutes?: number | null;
    ofpLandingLocation?: string | null;
    paxBriefingText?: string | null;
    paxBriefingVersion?: string | null;
    paxBriefingIsCustom?: boolean;
};
