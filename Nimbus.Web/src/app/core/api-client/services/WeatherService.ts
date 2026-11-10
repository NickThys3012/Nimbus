/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { HourlyForecastDto } from '../models/HourlyForecastDto';
import type { MetarTafDto } from '../models/MetarTafDto';
import type { WindProfileDto } from '../models/WindProfileDto';
import type { WindSeriesDto } from '../models/WindSeriesDto';
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
    /**
     * @returns WindProfileDto OK
     * @throws ApiError
     */
    public getApiWeatherWindProfile({
        latitude,
        longitude,
        hourUtc,
        model,
    }: {
        latitude?: number | string,
        longitude?: number | string,
        hourUtc?: string,
        model?: string,
    }): Observable<WindProfileDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/weather/wind-profile',
            query: {
                'latitude': latitude,
                'longitude': longitude,
                'hourUtc': hourUtc,
                'model': model,
            },
        });
    }
    /**
     * @returns HourlyForecastDto OK
     * @throws ApiError
     */
    public getApiWeatherHourlyForecast({
        latitude,
        longitude,
        model,
    }: {
        latitude?: number | string,
        longitude?: number | string,
        model?: string,
    }): Observable<HourlyForecastDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/weather/hourly-forecast',
            query: {
                'latitude': latitude,
                'longitude': longitude,
                'model': model,
            },
        });
    }
    /**
     * @returns WindSeriesDto OK
     * @throws ApiError
     */
    public getApiWeatherWindSeries({
        latitude,
        longitude,
        fromUtc,
        toUtc,
        model,
    }: {
        latitude?: number | string,
        longitude?: number | string,
        fromUtc?: string,
        toUtc?: string,
        model?: string,
    }): Observable<WindSeriesDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/weather/wind-series',
            query: {
                'latitude': latitude,
                'longitude': longitude,
                'fromUtc': fromUtc,
                'toUtc': toUtc,
                'model': model,
            },
        });
    }
}
