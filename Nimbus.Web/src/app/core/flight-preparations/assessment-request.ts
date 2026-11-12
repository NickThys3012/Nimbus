import type { FlightAssessmentRequestDto } from '../api-client';
import type { LoadCalculationFormGroup } from './flight-preparation-form';

/** The meteo values that feed the Go/No-Go verdict; `null` means "not entered". */
export interface WeatherContext {
  windSpeedKt: number | null;
  visibilityKm: number | null;
  capeJPerKg: number | null;
}

/**
 * The raw values sent to the server's single assessment (issue #50). The client never computes weight, lift sufficiency or the
 * verdict itself, so the screen, the print view and the export cannot diverge.
 */
export function buildAssessmentRequest(
  load: ReturnType<LoadCalculationFormGroup['getRawValue']>,
  pilotWeightKg: number,
  weather: WeatherContext,
): FlightAssessmentRequestDto {
  return {
    envelopeWeightKg: Number(load.envelopeWeightKg) || 0,
    basketWeightKg: Number(load.basketWeightKg) || 0,
    burnerWeightKg: Number(load.burnerWeightKg) || 0,
    cylindersWeightKg: Number(load.cylindersWeightKg) || 0,
    pilotWeightKg,
    passengerWeightsKg: load.passengers.map((p) => Number(p.weightKg) || 0),
    totalLiftKg: Number(load.totalLiftKg) || 0,
    windSpeedKt: weather.windSpeedKt,
    visibilityKm: weather.visibilityKm,
    capeJPerKg: weather.capeJPerKg,
  };
}
