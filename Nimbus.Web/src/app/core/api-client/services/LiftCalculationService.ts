/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { TotalLiftDto } from '../models/TotalLiftDto';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class LiftCalculationService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns TotalLiftDto OK
     * @throws ApiError
     */
    public getApiLiftCalculationTotalLift({
        maxAltitudeMeters,
        siteElevationMeters,
        ambientTemperatureC,
        internalTemperatureC,
        envelopeVolumeM3,
    }: {
        maxAltitudeMeters?: number | string,
        siteElevationMeters?: number | string,
        ambientTemperatureC?: number | string,
        internalTemperatureC?: number | string,
        envelopeVolumeM3?: number | string,
    }): Observable<TotalLiftDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/lift-calculation/total-lift',
            query: {
                'maxAltitudeMeters': maxAltitudeMeters,
                'siteElevationMeters': siteElevationMeters,
                'ambientTemperatureC': ambientTemperatureC,
                'internalTemperatureC': internalTemperatureC,
                'envelopeVolumeM3': envelopeVolumeM3,
            },
        });
    }
}
