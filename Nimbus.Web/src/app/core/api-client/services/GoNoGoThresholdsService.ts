/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { GoNoGoThresholdsDto } from '../models/GoNoGoThresholdsDto';
import type { GoNoGoThresholdsRequestDto } from '../models/GoNoGoThresholdsRequestDto';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class GoNoGoThresholdsService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns GoNoGoThresholdsDto OK
     * @throws ApiError
     */
    public getApiGoNoGoThresholds(): Observable<GoNoGoThresholdsDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/go-no-go-thresholds',
        });
    }
    /**
     * @returns GoNoGoThresholdsDto OK
     * @throws ApiError
     */
    public putApiGoNoGoThresholds({
        requestBody,
    }: {
        requestBody: GoNoGoThresholdsRequestDto,
    }): Observable<GoNoGoThresholdsDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/go-no-go-thresholds',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns GoNoGoThresholdsDto OK
     * @throws ApiError
     */
    public deleteApiGoNoGoThresholds(): Observable<GoNoGoThresholdsDto> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/go-no-go-thresholds',
        });
    }
}
