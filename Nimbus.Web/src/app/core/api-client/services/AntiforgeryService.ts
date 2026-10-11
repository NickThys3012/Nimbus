/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class AntiforgeryService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns any OK
     * @throws ApiError
     */
    public getApiAntiforgery(): Observable<any> {
        return this.httpRequest.request({
            method: 'GET',
            url: '/api/Antiforgery',
        });
    }
}
