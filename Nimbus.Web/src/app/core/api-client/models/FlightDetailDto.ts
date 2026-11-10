/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { FlightAssessmentDto } from './FlightAssessmentDto';
import type { FlightImageDto } from './FlightImageDto';
import type { FlightPermissionsDto } from './FlightPermissionsDto';
import type { FlightPreparationRequestDto } from './FlightPreparationRequestDto';
export type FlightDetailDto = {
    id: string;
    status: string;
    flownAt: string | null;
    viewerRole: string;
    permissions: FlightPermissionsDto;
    ownerName: string | null;
    balloonRegistration: string | null;
    balloonType: string | null;
    pilotName: string | null;
    launchLocationName: string | null;
    launchLocationIcaoCode: string | null;
    assessment: FlightAssessmentDto;
    details: FlightPreparationRequestDto;
    images: Array<FlightImageDto>;
};

