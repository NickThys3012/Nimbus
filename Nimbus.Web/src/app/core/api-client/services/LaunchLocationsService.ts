/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { LaunchLocationDto } from '../models/LaunchLocationDto';
import type { LaunchLocationRequestDto } from '../models/LaunchLocationRequestDto';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class LaunchLocationsService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns LaunchLocationDto OK
     * @throws ApiError
     */
    public getApiLaunchLocations(): Observable<Array<LaunchLocationDto>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/launch-locations',
        });
    }
    /**
     * @returns LaunchLocationDto OK
     * @throws ApiError
     */
    public postApiLaunchLocations({
        requestBody,
    }: {
        requestBody: LaunchLocationRequestDto,
    }): Observable<LaunchLocationDto> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/launch-locations',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns LaunchLocationDto OK
     * @throws ApiError
     */
    public getApiLaunchLocations1({
        id,
    }: {
        id: string,
    }): Observable<LaunchLocationDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/launch-locations/{id}',
            path: {
                'id': id,
            },
        });
    }
    /**
     * @returns LaunchLocationDto OK
     * @throws ApiError
     */
    public putApiLaunchLocations({
        id,
        requestBody,
    }: {
        id: string,
        requestBody: LaunchLocationRequestDto,
    }): Observable<LaunchLocationDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/launch-locations/{id}',
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
    public deleteApiLaunchLocations({
        id,
    }: {
        id: string,
    }): Observable<any> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/launch-locations/{id}',
            path: {
                'id': id,
            },
        });
    }
}
