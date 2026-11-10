/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import { NgModule} from '@angular/core';
import { HttpClientModule } from '@angular/common/http';
import { AngularHttpRequest } from './core/AngularHttpRequest';
import { BaseHttpRequest } from './core/BaseHttpRequest';
import type { OpenAPIConfig } from './core/OpenAPI';
import { OpenAPI } from './core/OpenAPI';
import { AdminService } from './services/AdminService';
import { AuthenticationService } from './services/AuthenticationService';
import { BalloonsService } from './services/BalloonsService';
import { FlightAssessmentService } from './services/FlightAssessmentService';
import { FlightPreparationsService } from './services/FlightPreparationsService';
import { GoNoGoThresholdsService } from './services/GoNoGoThresholdsService';
import { LaunchLocationsService } from './services/LaunchLocationsService';
import { LiftCalculationService } from './services/LiftCalculationService';
import { OfpSettingsService } from './services/OfpSettingsService';
import { PilotsService } from './services/PilotsService';
import { ProfileService } from './services/ProfileService';
import { SolarService } from './services/SolarService';
import { TelemetryService } from './services/TelemetryService';
import { WeatherService } from './services/WeatherService';
@NgModule({
    imports: [HttpClientModule],
    providers: [
        {
            provide: OpenAPI,
            useValue: {
                BASE: OpenAPI?.BASE ?? 'http://localhost:5214',
                VERSION: OpenAPI?.VERSION ?? '1.0.0',
                WITH_CREDENTIALS: OpenAPI?.WITH_CREDENTIALS ?? false,
                CREDENTIALS: OpenAPI?.CREDENTIALS ?? 'include',
                TOKEN: OpenAPI?.TOKEN,
                USERNAME: OpenAPI?.USERNAME,
                PASSWORD: OpenAPI?.PASSWORD,
                HEADERS: OpenAPI?.HEADERS,
                ENCODE_PATH: OpenAPI?.ENCODE_PATH,
            } as OpenAPIConfig,
        },
        {
            provide: BaseHttpRequest,
            useClass: AngularHttpRequest,
        },
        AdminService,
        AuthenticationService,
        BalloonsService,
        FlightAssessmentService,
        FlightPreparationsService,
        GoNoGoThresholdsService,
        LaunchLocationsService,
        LiftCalculationService,
        OfpSettingsService,
        PilotsService,
        ProfileService,
        SolarService,
        TelemetryService,
        WeatherService,
    ]
})
export class NimbusApiClient {}

