/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { OfpSettingsDto } from '../models/OfpSettingsDto';
import type { OfpSettingsRequestDto } from '../models/OfpSettingsRequestDto';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class OfpSettingsService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns OfpSettingsDto OK
     * @throws ApiError
     */
    public getApiOfpSettings(): Observable<OfpSettingsDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/ofp-settings',
        });
    }
    /**
     * @returns OfpSettingsDto OK
     * @throws ApiError
     */
    public putApiOfpSettings({
        requestBody,
    }: {
        requestBody: OfpSettingsRequestDto,
    }): Observable<OfpSettingsDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/ofp-settings',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns OfpSettingsDto OK
     * @throws ApiError
     */
    public deleteApiOfpSettings(): Observable<OfpSettingsDto> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/ofp-settings',
        });
    }
}
