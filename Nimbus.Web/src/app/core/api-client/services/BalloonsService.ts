/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { BalloonDto } from '../models/BalloonDto';
import type { BalloonRequestDto } from '../models/BalloonRequestDto';
import type { BalloonUsageDto } from '../models/BalloonUsageDto';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class BalloonsService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns BalloonDto OK
     * @throws ApiError
     */
    public getApiBalloons(): Observable<Array<BalloonDto>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/balloons',
        });
    }
    /**
     * @returns BalloonDto OK
     * @throws ApiError
     */
    public postApiBalloons({
        requestBody,
    }: {
        requestBody: BalloonRequestDto,
    }): Observable<BalloonDto> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/balloons',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns BalloonDto OK
     * @throws ApiError
     */
    public getApiBalloons1({
        id,
    }: {
        id: string,
    }): Observable<BalloonDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/balloons/{id}',
            path: {
                'id': id,
            },
        });
    }
    /**
     * @returns BalloonDto OK
     * @throws ApiError
     */
    public putApiBalloons({
        id,
        requestBody,
    }: {
        id: string,
        requestBody: BalloonRequestDto,
    }): Observable<BalloonDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/balloons/{id}',
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
    public deleteApiBalloons({
        id,
    }: {
        id: string,
    }): Observable<any> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/balloons/{id}',
            path: {
                'id': id,
            },
        });
    }
    /**
     * @returns BalloonUsageDto OK
     * @throws ApiError
     */
    public getApiBalloonsUsage({
        id,
    }: {
        id: string,
    }): Observable<BalloonUsageDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/balloons/{id}/usage',
            path: {
                'id': id,
            },
        });
    }
}
