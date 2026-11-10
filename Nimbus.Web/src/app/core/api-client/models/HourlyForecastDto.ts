/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { ForecastDayDto } from './ForecastDayDto';
import type { HourlyForecastHourDto } from './HourlyForecastHourDto';
export type HourlyForecastDto = {
    model: string;
    timeZone: string;
    utcOffsetSeconds: number | string;
    days: Array<ForecastDayDto>;
    hours: Array<HourlyForecastHourDto>;
};

