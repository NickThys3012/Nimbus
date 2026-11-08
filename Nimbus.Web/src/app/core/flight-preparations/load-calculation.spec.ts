import { calculateLoad } from './load-calculation';

const base = {
  envelopeWeightKg: 100,
  basketWeightKg: 50,
  burnerWeightKg: 20,
  cylindersWeightKg: 30,
  pilotWeightKg: 80,
  passengerWeightsKg: [70, 60],
  totalLiftKg: 0,
};

describe('calculateLoad', () => {
  it('sums equipment, pilot and passengers', () => {
    const r = calculateLoad(base);
    expect(r.passengersKg).toBe(130);
    expect(r.totalWeightKg).toBe(410);
  });

  it('has no verdict until a total lift is entered', () => {
    expect(calculateLoad(base).liftExceedsWeight).toBeNull();
  });

  it('flags lift that exceeds the total weight and reports the margin', () => {
    const r = calculateLoad({ ...base, totalLiftKg: 468 });
    expect(r.liftExceedsWeight).toBe(true);
    expect(r.marginKg).toBe(58);
  });

  it('flags lift that does not exceed the total weight (equal counts as not loadable)', () => {
    expect(calculateLoad({ ...base, totalLiftKg: 410 }).liftExceedsWeight).toBe(false);
    expect(calculateLoad({ ...base, totalLiftKg: 300 }).marginKg).toBe(-110);
  });

  it('treats blank or invalid numbers as zero', () => {
    const r = calculateLoad({ ...base, pilotWeightKg: NaN, passengerWeightsKg: [Number(''), 10] });
    expect(r.totalWeightKg).toBe(210);
  });
});
