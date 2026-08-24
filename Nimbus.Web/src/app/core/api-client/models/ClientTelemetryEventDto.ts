/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { ClientTelemetryEventType } from './ClientTelemetryEventType';
export type ClientTelemetryEventDto = {
    type: ClientTelemetryEventType;
    url: string;
    message?: string | null;
    stack?: string | null;
};

