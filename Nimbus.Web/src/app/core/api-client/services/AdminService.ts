/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
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
}
