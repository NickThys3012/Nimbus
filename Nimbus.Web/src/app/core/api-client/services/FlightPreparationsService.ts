/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { FlightListFilter } from '../models/FlightListFilter';
import type { FlightListPageDto } from '../models/FlightListPageDto';
import type { FlightListSort } from '../models/FlightListSort';
import type { FlightPreparationDto } from '../models/FlightPreparationDto';
import type { FlightPreparationRequestDto } from '../models/FlightPreparationRequestDto';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class FlightPreparationsService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns FlightListPageDto OK
     * @throws ApiError
     */
    public getApiFlightPreparations({
        filter,
        sort,
        descending = true,
        page,
        pageSize,
    }: {
        filter?: FlightListFilter,
        sort?: FlightListSort,
        descending?: boolean,
        page?: number | string,
        pageSize?: number | string,
    }): Observable<FlightListPageDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/flight-preparations',
            query: {
                'filter': filter,
                'sort': sort,
                'descending': descending,
                'page': page,
                'pageSize': pageSize,
            },
        });
    }
    /**
     * @returns FlightPreparationDto OK
     * @throws ApiError
     */
    public postApiFlightPreparations({
        requestBody,
    }: {
        requestBody: FlightPreparationRequestDto,
    }): Observable<FlightPreparationDto> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/flight-preparations',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns FlightPreparationDto OK
     * @throws ApiError
     */
    public getApiFlightPreparations1({
        id,
    }: {
        id: string,
    }): Observable<FlightPreparationDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/flight-preparations/{id}',
            path: {
                'id': id,
            },
        });
    }
    /**
     * @returns FlightPreparationDto OK
     * @throws ApiError
     */
    public putApiFlightPreparations({
        id,
        requestBody,
    }: {
        id: string,
        requestBody: FlightPreparationRequestDto,
    }): Observable<FlightPreparationDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/flight-preparations/{id}',
            path: {
                'id': id,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
