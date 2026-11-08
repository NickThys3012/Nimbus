export interface LoadCalculationInput {
  envelopeWeightKg: number;
  basketWeightKg: number;
  burnerWeightKg: number;
  cylindersWeightKg: number;
  /** The selected pilot's weight; 0 when no pilot is picked yet. */
  pilotWeightKg: number;
  passengerWeightsKg: readonly number[];
  totalLiftKg: number;
}

export interface LoadCalculationResult {
  passengersKg: number;
  totalWeightKg: number;
  /** Lift minus total weight; positive means spare lift. */
  marginKg: number;
  /** `null` while no total lift is entered, so the UI can prompt instead of claiming "overloaded". */
  liftExceedsWeight: boolean | null;
}

const num = (v: unknown): number => {
  const n = Number(v);
  return Number.isFinite(n) ? n : 0;
};

/**
 * Derives the load figures. Everything returned here is computed on demand from the typed-in
 * inputs and is never persisted as a source of truth.
 */
export function calculateLoad(input: LoadCalculationInput): LoadCalculationResult {
  const passengersKg = input.passengerWeightsKg.reduce<number>((sum, w) => sum + num(w), 0);
  const totalWeightKg =
    num(input.envelopeWeightKg) +
    num(input.basketWeightKg) +
    num(input.burnerWeightKg) +
    num(input.cylindersWeightKg) +
    num(input.pilotWeightKg) +
    passengersKg;
  const totalLiftKg = num(input.totalLiftKg);
  const marginKg = totalLiftKg - totalWeightKg;
  return { passengersKg, totalWeightKg, marginKg, liftExceedsWeight: totalLiftKg > 0 ? marginKg > 0 : null };
}
