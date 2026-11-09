/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { FlightAssessmentDto } from '../models/FlightAssessmentDto';
import type { FlightAssessmentRequestDto } from '../models/FlightAssessmentRequestDto';
import { BaseHttpRequest } from '../core/BaseHttpRequest';
@Injectable({
    providedIn: 'root',
})
export class FlightAssessmentService {
    constructor(public readonly httpRequest: BaseHttpRequest) {}
    /**
     * @returns FlightAssessmentDto OK
     * @throws ApiError
     */
    public postApiFlightAssessment({
        requestBody,
    }: {
        requestBody: FlightAssessmentRequestDto,
    }): Observable<FlightAssessmentDto> {
        return this.httpRequest.request({
            method: 'POST',
            url: '/api/flight-assessment',
            body: requestBody,
            mediaType: 'application/json',
        });
    }
}
