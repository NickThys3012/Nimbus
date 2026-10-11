/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { CurrentUserDto } from '../models/CurrentUserDto';
import type { LoginRequestDto } from '../models/LoginRequestDto';
import type { LoginResponseDto } from '../models/LoginResponseDto';
import type { RegisterRequestDto } from '../models/RegisterRequestDto';
import type { ResendVerificationEmailCommandDto } from '../models/ResendVerificationEmailCommandDto';
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
     * @returns any OK
     * @throws ApiError
     */
    public postApiAuthenticationRegister({
        requestBody,
    }: {
        requestBody: RegisterRequestDto,
    }): Observable<any> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/Authentication/register',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns any OK
     * @throws ApiError
     */
    public postApiAuthenticationResendVerificationEmail({
        requestBody,
    }: {
        requestBody: ResendVerificationEmailCommandDto,
    }): Observable<any> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/Authentication/resend-verification-email',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns any OK
     * @throws ApiError
     */
    public getApiAuthenticationConfirmEmail({
        userId,
        token,
    }: {
        userId?: string,
        token?: string,
    }): Observable<any> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/Authentication/confirm-email',
            query: {
                'userId': userId,
                'token': token,
            },
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
    /**
     * @returns CurrentUserDto OK
     * @throws ApiError
     */
    public getApiAuthenticationMe(): Observable<CurrentUserDto> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/Authentication/me',
        });
    }
}
