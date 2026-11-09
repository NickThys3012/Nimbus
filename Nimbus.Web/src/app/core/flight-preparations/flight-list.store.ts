import { computed, inject, Injectable, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';

import { FlightPreparationsService } from '../api-client';

/** Values of the API's `FlightListFilter` / `FlightListSort` enums (generated as plain numbers). */
export const FlightFilter = { all: 0, flown: 1, notFlown: 2, shared: 3 } as const;
export const FlightSort = { date: 0, balloon: 1, location: 2, status: 3 } as const;
export type FlightFilterValue = (typeof FlightFilter)[keyof typeof FlightFilter];
export type FlightSortValue = (typeof FlightSort)[keyof typeof FlightSort];

export const FLIGHT_PAGE_SIZE = 20;

/**
 * State of the flight overview: the query (filter, sort, page) lives in signals and the list is an
 * `rxResource` that refetches whenever one changes. Filtering, sorting and paging all happen on the
 * server (the API scopes the result to the signed-in user and what is shared with them).
 */
@Injectable({ providedIn: 'root' })
export class FlightListStore {
  private readonly api = inject(FlightPreparationsService);

  readonly filter = signal<FlightFilterValue>(FlightFilter.all);
  readonly sort = signal<FlightSortValue>(FlightSort.date);
  readonly descending = signal(true);
  readonly page = signal(1);

  readonly flights = rxResource({
    params: () => ({ filter: this.filter(), sort: this.sort(), descending: this.descending(), page: this.page() }),
    stream: ({ params }) => this.api.getApiFlightPreparations({ ...params, pageSize: FLIGHT_PAGE_SIZE }),
  });

  readonly items = computed(() => (this.flights.hasValue() ? this.flights.value().items : []));
  readonly totalCount = computed(() => (this.flights.hasValue() ? Number(this.flights.value().totalCount) : 0));
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.totalCount() / FLIGHT_PAGE_SIZE)));

  setFilter(filter: FlightFilterValue): void {
    this.filter.set(filter);
    this.page.set(1);
  }

  setSort(sort: FlightSortValue): void {
    this.sort.set(sort);
    // Date is most useful newest first; the text columns read naturally A-Z.
    this.descending.set(sort === FlightSort.date);
    this.page.set(1);
  }

  toggleDirection(): void {
    this.descending.update((d) => !d);
    this.page.set(1);
  }

  goToPage(page: number): void {
    this.page.set(Math.min(Math.max(1, page), this.pageCount()));
  }
}
