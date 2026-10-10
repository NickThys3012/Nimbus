import { formatDayMonth, formatDayMonthTime, formatDayMonthYear } from './dates';

describe('dates', () => {
  const iso = '2026-07-26T09:14:00';

  it('formats day and month without a trailing dot', () => {
    expect(formatDayMonth(iso)).toBe('26 jul');
  });

  it('adds the year', () => {
    expect(formatDayMonthYear(iso)).toBe('26 jul 2026');
  });

  it('adds the time', () => {
    expect(formatDayMonthTime(iso)).toMatch(/^26 jul,? 09:14$/);
  });

  it('returns an empty string for an invalid date', () => {
    expect(formatDayMonth('nope')).toBe('');
  });
});
