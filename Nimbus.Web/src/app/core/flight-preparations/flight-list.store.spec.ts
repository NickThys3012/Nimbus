import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { type Mock, vi } from 'vitest';

import { FlightPreparationsService, type FlightListPageDto } from '../api-client';
import { FLIGHT_PAGE_SIZE, FlightFilter, FlightListStore, FlightSort } from './flight-list.store';

const page = (totalCount: number): FlightListPageDto => ({ items: [], totalCount, page: 1, pageSize: FLIGHT_PAGE_SIZE });

describe('FlightListStore', () => {
  let api: { getApiFlightPreparations: Mock };
  let store: FlightListStore;

  async function setup(total = 45) {
    api = { getApiFlightPreparations: vi.fn().mockReturnValue(of(page(total))) };
    TestBed.configureTestingModule({ providers: [{ provide: FlightPreparationsService, useValue: api }] });
    store = TestBed.inject(FlightListStore);
    TestBed.tick();
    await new Promise((r) => setTimeout(r));
  }

  const lastCall = () => api.getApiFlightPreparations.mock.calls.at(-1)![0];

  it('requests the first page, newest first, with a bounded page size', async () => {
    await setup();
    expect(lastCall()).toEqual({ filter: 0, sort: 0, descending: true, page: 1, pageSize: FLIGHT_PAGE_SIZE });
    expect(store.pageCount()).toBe(3);
  });

  it('sends filter, sort and paging to the server and resets to page 1 on a filter change', async () => {
    await setup();
    store.goToPage(2);
    TestBed.tick();
    await new Promise((r) => setTimeout(r));
    expect(lastCall().page).toBe(2);

    store.setFilter(FlightFilter.shared);
    TestBed.tick();
    await new Promise((r) => setTimeout(r));
    expect(lastCall()).toMatchObject({ filter: FlightFilter.shared, page: 1 });

    store.setSort(FlightSort.balloon);
    TestBed.tick();
    await new Promise((r) => setTimeout(r));
    expect(lastCall()).toMatchObject({ sort: FlightSort.balloon, descending: false, page: 1 });
  });

  it('clamps the page to the available range', async () => {
    await setup(45);
    store.goToPage(99);
    expect(store.page()).toBe(3);
    store.goToPage(-4);
    expect(store.page()).toBe(1);
  });
});
