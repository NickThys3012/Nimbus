import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthStore } from '../../core/auth/auth.store';
import type { FlightListItemDto } from '../../core/api-client';
import {
  FlightFilter,
  type FlightFilterValue,
  FlightListStore,
  FlightSort,
  type FlightSortValue,
} from '../../core/flight-preparations/flight-list.store';

const WEEKDAYS = ['SU', 'MO', 'TU', 'WE', 'TH', 'FR', 'SA'];

@Component({
  selector: 'Nimbus-flight-overview',
  imports: [RouterLink],
  templateUrl: './flight-overview.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './flight-overview.css',
})
export default class FlightOverview {
  protected readonly store = inject(FlightListStore);
  protected readonly auth = inject(AuthStore);

  protected readonly filters: { value: FlightFilterValue; label: string; testId: string }[] = [
    { value: FlightFilter.all, label: 'All', testId: 'filter-all' },
    { value: FlightFilter.flown, label: 'Flown', testId: 'filter-flown' },
    { value: FlightFilter.notFlown, label: 'Not flown', testId: 'filter-not-flown' },
    { value: FlightFilter.shared, label: 'Shared with me', testId: 'filter-shared' },
  ];
  protected readonly sorts: { value: FlightSortValue; label: string }[] = [
    { value: FlightSort.date, label: 'Date' },
    { value: FlightSort.balloon, label: 'Balloon' },
    { value: FlightSort.location, label: 'Location' },
    { value: FlightSort.status, label: 'Flown state' },
  ];

  protected onSort(event: Event): void {
    this.store.setSort(Number((event.target as HTMLSelectElement).value) as FlightSortValue);
  }

  /** "FR 08" from an ISO date (no time zone shifting: the date is a calendar date). */
  protected dayLabel(flight: FlightListItemDto): string {
    const [y, m, d] = flight.flightDate.split('-').map(Number);
    return `${WEEKDAYS[new Date(y, m - 1, d).getDay()]} ${String(d).padStart(2, '0')}`;
  }

  protected timeLabel(flight: FlightListItemDto): string {
    return flight.flightTime.slice(0, 5);
  }

  protected verdictLabel(verdict: string): string {
    switch (verdict) {
      case 'green':
        return 'GO';
      case 'yellow':
        return 'CAUTION';
      case 'red':
        return 'NO-GO';
      default:
        return 'N/A';
    }
  }

  protected verdictClass(verdict: string): string {
    switch (verdict) {
      case 'green':
        return 'go';
      case 'yellow':
        return 'caution';
      case 'red':
        return 'nogo';
      default:
        return 'unknown';
    }
  }
}
