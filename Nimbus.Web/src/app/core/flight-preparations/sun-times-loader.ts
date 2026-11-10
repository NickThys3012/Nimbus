import { computed, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { SolarService, type SunTimesDto } from '../api-client';

export interface SunControl {
  value: string;
  setValue(value: string): void;
  markAsDirty(): void;
}

/** The time zone the pilot is in: the flight's date and time are read in it. */
export function browserTimeZone(): string | null {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || null;
  } catch {
    return null;
  }
}

/**
 * Sunrise and sunset of the launch location on the flight's date, from the API's NOAA calculation (issue #49). It fills the
 * form's Sunrise and Sunset fields, but never over something the pilot typed: a field is only written when it is empty or still
 * holds what this loader wrote last, so changing the date or the location refreshes it and a hand-entered time stays.
 */
@Injectable()
export class SunTimesLoader {
  private readonly solar = inject(SolarService);

  readonly coordinates = signal<{ latitude: number; longitude: number } | null>(null);
  /** The flight's date, `yyyy-MM-dd`, or empty. */
  readonly date = signal('');
  readonly timeZone = signal<string | null>(browserTimeZone());

  readonly times = signal<SunTimesDto | null>(null);
  readonly failed = signal(false);

  /** Short line under the fields: what the times are, or why there are none. */
  readonly note = computed(() => {
    const t = this.times();
    if (this.failed()) return "Couldn't calculate sunrise and sunset right now. Enter them by hand.";
    if (!t) return null;
    if (t.condition === 'polarDay') return 'Polar day: the sun does not set on this date.';
    if (t.condition === 'polarNight') return 'Polar night: the sun does not rise on this date.';
    return `Calculated for ${t.date}${t.timeZone ? ` (${t.timeZone})` : ''} with the NOAA solar-position algorithm.`;
  });

  private lastApplied: { sunrise: string; sunset: string } | null = null;
  private requestId = 0;

  constructor() {
    effect(() => {
      const at = this.coordinates();
      const date = this.date();
      const timeZone = this.timeZone();
      untracked(() => void this.load(at, date, timeZone));
    });
  }

  private async load(at: { latitude: number; longitude: number } | null, date: string, timeZone: string | null): Promise<void> {
    const id = ++this.requestId;
    if (!at || !/^\d{4}-\d{2}-\d{2}$/.test(date)) {
      this.times.set(null);
      this.failed.set(false);
      return;
    }
    try {
      const result = await firstValueFrom(
        this.solar.getApiSolarSunTimes({ latitude: at.latitude, longitude: at.longitude, date, timeZone: timeZone ?? undefined }),
      );
      if (id !== this.requestId) return; // a newer date or place was asked for meanwhile
      this.failed.set(false);
      this.times.set(result);
    } catch {
      if (id !== this.requestId) return;
      this.times.set(null);
      this.failed.set(true);
    }
  }

  /** Writes the calculated times into the fields they belong to, unless the pilot has put something else there. */
  fill(times: SunTimesDto | null, sunrise: SunControl, sunset: SunControl): void {
    if (!times || times.condition !== 'normal' || !times.sunrise || !times.sunset) return;
    const mine = this.lastApplied;
    const write = (control: SunControl, value: string, previous: string | undefined) => {
      if (control.value === value) return;
      if (control.value && control.value !== previous) return;
      control.setValue(value);
      control.markAsDirty();
    };
    write(sunrise, times.sunrise, mine?.sunrise);
    write(sunset, times.sunset, mine?.sunset);
    this.lastApplied = { sunrise: times.sunrise, sunset: times.sunset };
  }
}
