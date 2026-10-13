/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { ProfileDto } from '../models/ProfileDto';
import type { ProfileRequestDto } from '../models/ProfileRequestDto';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class ProfileService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns ProfileDto OK
     * @throws ApiError
     */
    public getApiProfile(): Observable<ProfileDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/profile',
        });
    }
    /**
     * @returns ProfileDto OK
     * @throws ApiError
     */
    public putApiProfile({
        requestBody,
    }: {
        requestBody: ProfileRequestDto,
    }): Observable<ProfileDto> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/profile',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
