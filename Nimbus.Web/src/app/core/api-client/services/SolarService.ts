/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { SunTimesDto } from '../models/SunTimesDto';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class SolarService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns SunTimesDto OK
     * @throws ApiError
     */
    public getApiSolarSunTimes({
        latitude,
        longitude,
        date,
        timeZone,
    }: {
        latitude?: number | string,
        longitude?: number | string,
        date?: string,
        timeZone?: string,
    }): Observable<SunTimesDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/solar/sun-times',
            query: {
                'latitude': latitude,
                'longitude': longitude,
                'date': date,
                'timeZone': timeZone,
            },
        });
    }
}
