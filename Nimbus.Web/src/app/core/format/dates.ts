const LOCALE = 'nl-BE';

function parts(iso: string, options: Intl.DateTimeFormatOptions): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  // nl-BE abbreviates months with a trailing dot ("jul."); the design omits it.
  return new Intl.DateTimeFormat(LOCALE, options).format(date).replace(/\./g, '');
}

/** "26 jul" */
export const formatDayMonth = (iso: string): string =>
  parts(iso, { day: 'numeric', month: 'short' });

/** "26 jul 2026" */
export const formatDayMonthYear = (iso: string): string =>
  parts(iso, { day: 'numeric', month: 'short', year: 'numeric' });

/** "28 jul 09:14" (local time) */
export const formatDayMonthTime = (iso: string): string =>
  parts(iso, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', hour12: false });
