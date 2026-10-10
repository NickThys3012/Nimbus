/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { AdminUserDto } from '../models/AdminUserDto';
import type { LoginEventDto } from '../models/LoginEventDto';
import type { SetUserRoleRequestDto } from '../models/SetUserRoleRequestDto';
import type { UnblockVerificationEmailRequestDto } from '../models/UnblockVerificationEmailRequestDto';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class AdminService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns any OK
     * @throws ApiError
     */
    public postApiAdminVerificationEmailUnblock({
        requestBody,
    }: {
        requestBody: UnblockVerificationEmailRequestDto,
    }): Observable<any> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/admin/verification-email/unblock',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
    /**
     * @returns AdminUserDto OK
     * @throws ApiError
     */
    public getApiAdminUsers(): Observable<Array<AdminUserDto>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/admin/users',
        });
    }
    /**
     * @returns any OK
     * @throws ApiError
     */
    public postApiAdminUsersApprove({
        id,
    }: {
        id: string,
    }): Observable<any> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/admin/users/{id}/approve',
            path: {
                'id': id,
            },
        });
    }
    /**
     * @returns any OK
     * @throws ApiError
     */
    public postApiAdminUsersReject({
        id,
    }: {
        id: string,
    }): Observable<any> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/admin/users/{id}/reject',
            path: {
                'id': id,
            },
        });
    }
    /**
     * @returns any OK
     * @throws ApiError
     */
    public putApiAdminUsersRole({
        id,
        requestBody,
    }: {
        id: string,
        requestBody: SetUserRoleRequestDto,
    }): Observable<any> {
        return this.httpRequest.request({
            method: 'PUT',
            url: '/api/admin/users/{id}/role',
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
    public postApiAdminUsersBlock({
        id,
    }: {
        id: string,
    }): Observable<any> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/admin/users/{id}/block',
            path: {
                'id': id,
            },
        });
    }
    /**
     * @returns any OK
     * @throws ApiError
     */
    public postApiAdminUsersUnblock({
        id,
    }: {
        id: string,
    }): Observable<any> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/admin/users/{id}/unblock',
            path: {
                'id': id,
            },
        });
    }
    /**
     * @returns any OK
     * @throws ApiError
     */
    public deleteApiAdminUsers({
        id,
    }: {
        id: string,
    }): Observable<any> {
        return this.httpRequest.request({
            method: 'DELETE',
            url: '/api/admin/users/{id}',
            path: {
                'id': id,
            },
        });
    }
    /**
     * @returns LoginEventDto OK
     * @throws ApiError
     */
    public getApiAdminLoginEvents({
        limit,
    }: {
        limit?: number | string,
    }): Observable<Array<LoginEventDto>> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/admin/login-events',
            query: {
                'limit': limit,
            },
        });
    }
}
