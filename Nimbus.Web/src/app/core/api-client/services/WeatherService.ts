/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { MetarTafDto } from '../models/MetarTafDto';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class WeatherService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns MetarTafDto OK
     * @throws ApiError
     */
    public getApiWeatherMetarTaf({
        icao,
    }: {
        icao?: string,
    }): Observable<MetarTafDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/weather/metar-taf',
            query: {
                'icao': icao,
            },
        });
    }
}
