import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';

import { LocationMap } from './location-map';

const calls = vi.hoisted(() => ({
  map: vi.fn(),
  setView: vi.fn(),
  remove: vi.fn(),
  marker: vi.fn(),
  setLatLng: vi.fn(),
}));

vi.mock('leaflet', () => {
  const container = { current: null as unknown };
  const map = {
    setView: (...a: unknown[]) => {
      calls.setView(...a);
      return map;
    },
    remove: calls.remove,
    getContainer: () => container.current,
  };
  const layer = { addTo: () => layer };
  return {
    map: (el: unknown, opts: unknown) => {
      container.current = el;
      calls.map(el, opts);
      return map;
    },
    tileLayer: () => layer,
    divIcon: () => ({}),
    marker: (p: unknown) => {
      calls.marker(p);
      return { addTo: () => ({ setLatLng: calls.setLatLng }), setLatLng: calls.setLatLng };
    },
  };
});

@Component({
  imports: [LocationMap],
  template: `<Nimbus-location-map [latitude]="lat()" [longitude]="lon()"></Nimbus-location-map>`,
})
class Host {
  lat = signal<number | null>(51.02);
  lon = signal<number | null>(4.1);
}

describe('LocationMap', () => {
  async function setup() {
    await TestBed.configureTestingModule({ imports: [Host] }).compileComponents();
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    await new Promise((r) => setTimeout(r)); // lazy leaflet import
    await fixture.whenStable();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  beforeEach(() => vi.clearAllMocks());

  it('renders a map centred on the coordinate with a marker', async () => {
    const { el } = await setup();
    expect(el.querySelector('[data-testid="location-map"]')).toBeTruthy();
    expect(calls.map).toHaveBeenCalledTimes(1);
    expect(calls.setView).toHaveBeenCalledWith([51.02, 4.1], 13);
    expect(calls.marker).toHaveBeenCalledWith([51.02, 4.1]);
  });

  it('moves the marker when the coordinate changes', async () => {
    const { fixture } = await setup();
    fixture.componentInstance.lat.set(50.5);
    await fixture.whenStable();
    expect(calls.setLatLng).toHaveBeenCalledWith([50.5, 4.1]);
    expect(calls.map).toHaveBeenCalledTimes(1);
  });

  it('shows a hint instead of a map for missing or out-of-range coordinates', async () => {
    const { fixture, el } = await setup();
    fixture.componentInstance.lat.set(95);
    await fixture.whenStable();
    expect(el.querySelector('[data-testid="location-map"]')).toBeNull();
    expect(el.querySelector('[data-testid="location-map-empty"]')).toBeTruthy();
    fixture.componentInstance.lat.set(null);
    await fixture.whenStable();
    expect(el.querySelector('[data-testid="location-map-empty"]')).toBeTruthy();
  });

  it('removes the map when destroyed', async () => {
    const { fixture } = await setup();
    fixture.destroy();
    expect(calls.remove).toHaveBeenCalled();
  });
});
