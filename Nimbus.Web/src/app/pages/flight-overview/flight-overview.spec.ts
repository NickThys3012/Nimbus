import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { type Mock, vi } from 'vitest';

import FlightOverview from './flight-overview';
import { type FlightListItemDto, FlightPreparationsService } from '../../core/api-client';
import { AuthStore } from '../../core/auth/auth.store';
import { signal } from '@angular/core';

const flight = (over: Partial<FlightListItemDto>): FlightListItemDto => ({
  id: 'f1', title: 'Weelde · evening flight', flightDate: '2026-08-08', flightTime: '18:45:00', balloonRegistration: 'OO-BLK',
  locationName: 'Weelde', isFlown: false, flownAt: null, goNoGoVerdict: 'green', isShared: false, ownerName: null, ...over,
});

describe('FlightOverview', () => {
  let api: { getApiFlightPreparations: Mock };

  async function setup(items: FlightListItemDto[], total = items.length) {
    api = { getApiFlightPreparations: vi.fn().mockReturnValue(of({ items, totalCount: total, page: 1, pageSize: 20 })) };
    await TestBed.configureTestingModule({
      imports: [FlightOverview],
      providers: [
        provideRouter([]),
        { provide: FlightPreparationsService, useValue: api },
        { provide: AuthStore, useValue: { abbreviatedName: signal('N. Thys') } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(FlightOverview);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const q = (id: string) => el.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
    return { fixture, el, q };
  }

  it('shows date, balloon, location, flown state and the verdict badge per row', async () => {
    const { q } = await setup([
      flight({}),
      flight({ id: 'f2', goNoGoVerdict: 'red', isFlown: true, balloonRegistration: null }),
      flight({ id: 'f3', goNoGoVerdict: 'yellow', isShared: true, ownerName: 'Bob Builder' }),
    ]);
    const row1 = q('flight-f1')!;
    expect(row1.textContent).toContain('SA 08');
    expect(row1.textContent).toContain('18:45');
    expect(row1.textContent).toContain('OO-BLK');
    expect(row1.textContent).toContain('Weelde');
    expect(row1.querySelector('[data-testid="verdict-badge"]')!.textContent).toContain('GO');
    expect(row1.querySelector('[data-testid="flown-badge"]')).toBeNull();
    expect(q('flight-f2')!.querySelector('[data-testid="flown-badge"]')).not.toBeNull();
    expect(q('flight-f2')!.querySelector('[data-testid="verdict-badge"]')!.textContent).toContain('NO-GO');
    expect(q('flight-f2')!.textContent).toContain('No balloon');
    expect(q('flight-f3')!.textContent).toContain('CAUTION');
    expect(q('flight-f3')!.textContent).toContain('shared by Bob Builder');
  });

  it('asks the server for the chosen filter and shows an empty state', async () => {
    const { fixture, q } = await setup([]);
    expect(q('empty')).not.toBeNull();
    q('filter-shared')!.click();
    await fixture.whenStable();
    expect(api.getApiFlightPreparations).toHaveBeenLastCalledWith(expect.objectContaining({ filter: 3, page: 1 }));
  });

  it('pages through the server', async () => {
    const { fixture, q } = await setup([flight({})], 45);
    expect(q('page-info')!.textContent).toContain('Page 1 of 3');
    expect((q('prev') as HTMLButtonElement).disabled).toBe(true);
    q('next')!.click();
    await fixture.whenStable();
    expect(api.getApiFlightPreparations).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2 }));
  });

  it('shows an error when loading fails', async () => {
    api = { getApiFlightPreparations: vi.fn().mockReturnValue(throwError(() => new Error('x'))) };
    await TestBed.configureTestingModule({
      imports: [FlightOverview],
      providers: [provideRouter([]), { provide: FlightPreparationsService, useValue: api }, { provide: AuthStore, useValue: { abbreviatedName: signal(null) } }],
    }).compileComponents();
    const fixture = TestBed.createComponent(FlightOverview);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="error"]')).not.toBeNull();
  });
});
