/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { PilotDto } from '../models/PilotDto';
import type { PilotRequestDto } from '../models/PilotRequestDto';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class PilotsService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns PilotDto OK
     * @throws ApiError
     */
    public getApiPilots(): Observable<Array<PilotDto>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/pilots',
        });
    }
    /**
     * @returns PilotDto OK
     * @throws ApiError
     */
    public postApiPilots({
        requestBody,
    }: {
        requestBody: PilotRequestDto,
    }): Observable<PilotDto> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/pilots',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns PilotDto OK
     * @throws ApiError
     */
    public getApiPilots1({
        id,
    }: {
        id: string,
    }): Observable<PilotDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/pilots/{id}',
            path: {
                'id': id,
            },
        });
    }
    /**
     * @returns PilotDto OK
     * @throws ApiError
     */
    public putApiPilots({
        id,
        requestBody,
    }: {
        id: string,
        requestBody: PilotRequestDto,
    }): Observable<PilotDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/pilots/{id}',
            path: {
                'id': id,
            },
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns any OK
     * @throws ApiError
     */
    public deleteApiPilots({
        id,
    }: {
        id: string,
    }): Observable<any> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/pilots/{id}',
            path: {
                'id': id,
            },
        });
    }
}
