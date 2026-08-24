/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { ClientTelemetryEventDto } from '../models/ClientTelemetryEventDto';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class TelemetryService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns any OK
     * @throws ApiError
     */
    public postApiTelemetry({
        requestBody,
    }: {
        requestBody?: (null | Array<ClientTelemetryEventDto>),
    }): Observable<any> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/Telemetry',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
