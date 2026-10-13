import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  viewChild,
} from '@angular/core';
import type { Map as LeafletMap, Marker } from 'leaflet';

/**
 * Small Leaflet preview of one coordinate, so a mistyped latitude or longitude is obvious at a glance.
 * Leaflet is loaded lazily (own chunk). With no valid coordinate the map is replaced by a hint.
 */
@Component({
  selector: 'Nimbus-location-map',
  templateUrl: './location-map.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './location-map.css',
})
export class LocationMap {
  readonly latitude = input<number | null>(null);
  readonly longitude = input<number | null>(null);

  /** The point to show, or null when either value is missing or outside the real ranges. */
  protected readonly point = computed<[number, number] | null>(() => {
    const lat = this.latitude();
    const lon = this.longitude();
    if (lat === null || lon === null || !Number.isFinite(lat) || !Number.isFinite(lon)) return null;
    return lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180 ? [lat, lon] : null;
  });

  /** Why there is no pin, shown in the placeholder. */
  protected readonly reason = computed(() => {
    const lat = this.latitude();
    const lon = this.longitude();
    if (lat !== null && Number.isFinite(lat) && Math.abs(lat) > 90) return 'LATITUDE OUT OF RANGE';
    if (lon !== null && Number.isFinite(lon) && Math.abs(lon) > 180) return 'LONGITUDE OUT OF RANGE';
    return 'ENTER COORDINATES';
  });

  private readonly container = viewChild<ElementRef<HTMLElement>>('map');
  private map: LeafletMap | null = null;
  private marker: Marker | null = null;
  private ready = false;
  private destroyed = false;
  private leaflet: typeof import('leaflet') | null = null;

  constructor() {
    const destroyRef = inject(DestroyRef);
    destroyRef.onDestroy(() => {
      this.destroyed = true;
      this.map?.remove();
      this.map = null;
    });

    afterNextRender(async () => {
      this.leaflet = await import('leaflet');
      this.ready = true;
      this.render();
    });

    effect(() => {
      this.point();
      this.container();
      if (this.ready) this.render();
    });
  }

  private render(): void {
    const L = this.leaflet;
    const el = this.container()?.nativeElement;
    const point = this.point();
    if (this.destroyed || !L) return;
    if (!point || !el) {
      this.map?.remove();
      this.map = null;
      this.marker = null;
      return;
    }
    if (!this.map || this.map.getContainer() !== el) {
      this.map?.remove();
      this.map = L.map(el, { zoomControl: true, attributionControl: true, scrollWheelZoom: false }).setView(point, 13);
      L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
      }).addTo(this.map);
      // A divIcon avoids Leaflet's default marker images, which the bundler does not resolve.
      this.marker = L.marker(point, {
        icon: L.divIcon({ className: 'nimbus-pin', html: '<i class="ti ti-map-pin-filled"></i>', iconSize: [34, 34], iconAnchor: [17, 34] }),
      }).addTo(this.map);
      return;
    }
    this.marker?.setLatLng(point);
    this.map.setView(point);
  }
}
