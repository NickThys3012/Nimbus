import { nextFlightSlot } from './flight-preparation-form';

describe('nextFlightSlot', () => {
  it('picks the evening slot when it is morning before the evening window', () => {
    expect(nextFlightSlot(new Date(2026, 7, 8, 9, 30))).toEqual({ flightDate: '2026-08-08', flightTime: '19:00' });
  });

  it('picks the morning slot of the same day when it is early enough', () => {
    expect(nextFlightSlot(new Date(2026, 7, 8, 5, 0))).toEqual({ flightDate: '2026-08-08', flightTime: '07:00' });
  });

  it('rolls over to tomorrow morning late in the evening, including month ends', () => {
    expect(nextFlightSlot(new Date(2026, 7, 31, 19, 30))).toEqual({ flightDate: '2026-09-01', flightTime: '07:00' });
  });

  it('requires at least an hour of lead time', () => {
    expect(nextFlightSlot(new Date(2026, 7, 8, 6, 30))).toEqual({ flightDate: '2026-08-08', flightTime: '19:00' });
  });
});
