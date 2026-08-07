/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { LoginRequestDto } from '../models/LoginRequestDto';
import type { LoginResponseDto } from '../models/LoginResponseDto';
import type { UserDto } from '../models/UserDto';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class AuthenticationService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns LoginResponseDto OK
     * @throws ApiError
     */
    public postApiAuthenticationLogin({
        requestBody,
    }: {
        requestBody: LoginRequestDto,
    }): Observable<LoginResponseDto> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/Authentication/login',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns LoginResponseDto OK
     * @throws ApiError
     */
    public postApiAuthenticationRefresh(): Observable<LoginResponseDto> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/Authentication/refresh',
        });
    }
    /**
     * @returns any OK
     * @throws ApiError
     */
    public postApiAuthenticationLogout(): Observable<any> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/Authentication/logout',
        });
    }
    /**
     * @returns UserDto OK
     * @throws ApiError
     */
    public getApiAuthentication({
        email,
    }: {
        email?: string,
    }): Observable<UserDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/Authentication',
            query: {
                'email': email,
            },
        });
    }
}
