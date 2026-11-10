/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { WindSnapshotDto } from './WindSnapshotDto';
export type WindSeriesDto = {
    model: string;
    requestedFromUtc: string;
    requestedToUtc: string;
    fromUtc: string | null;
    toUtc: string | null;
    shortfallHours: number | string;
    gapHoursUtc: Array<string>;
    providerAvailable: boolean;
    snapshots: Array<WindSnapshotDto>;
};

