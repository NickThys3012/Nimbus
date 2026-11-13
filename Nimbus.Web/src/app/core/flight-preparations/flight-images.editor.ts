import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import type { Subscription } from 'rxjs';

import type { FlightImageDto } from '../api-client';
import { FlightImagesApi } from './flight-images.api';

/** Mirrors the API's default `ImageUpload` limits; the API stays the authority, this only spares a pointless upload. */
export const ACCEPTED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const;
export const MAX_IMAGE_BYTES = 10 * 1024 * 1024;

/** One upload in flight, or one that failed and is waiting to be dismissed or retried. */
export interface UploadItem {
  id: number;
  fileName: string;
  percent: number;
  state: 'uploading' | 'failed';
  error: string | null;
  file: File;
}

/**
 * The images of one flight section, as the form edits them (issue #50): uploads with progress and cancel, reorder, remove.
 * It is independent of the dossier form, so a failed upload or delete only ever shows a message beside the images; nothing
 * the pilot typed is touched.
 */
@Injectable()
export class FlightImagesEditor {
  private readonly api = inject(FlightImagesApi);

  private flightId: string | null = null;
  private section = '';
  private nextId = 1;
  private readonly running = new Map<number, Subscription>();

  readonly images = signal<FlightImageDto[]>([]);
  readonly uploads = signal<UploadItem[]>([]);
  /** A problem with loading, reordering or removing; cleared by the next successful action. */
  readonly problem = signal<string | null>(null);
  readonly loading = signal(false);
  /** The image whose reorder/remove request is in flight; its buttons are disabled meanwhile. */
  readonly busyId = signal<string | null>(null);
  readonly hasFlight = computed(() => this.flightId !== null);

  /** Points the editor at a flight and section; null (a flight not saved yet) means images cannot be attached. */
  bind(flightId: string | null, section: string): void {
    if (this.flightId === flightId && this.section === section) return;
    this.cancelAll();
    this.flightId = flightId;
    this.section = section;
    this.images.set([]);
    this.uploads.set([]);
    this.problem.set(null);
    if (flightId) this.load(flightId);
  }

  private load(flightId: string): void {
    this.loading.set(true);
    this.api.list(flightId).subscribe({
      next: (all) => {
        if (this.flightId !== flightId) return;
        this.images.set(all.filter((i) => i.section === this.section).sort((a, b) => a.order - b.order));
        this.loading.set(false);
      },
      error: (e) => {
        if (this.flightId !== flightId) return;
        this.loading.set(false);
        this.problem.set(describe(e, 'The images could not be loaded.'));
      },
    });
  }

  /** Starts one upload per file; a file that is the wrong type or too large is refused straight away with its reason. */
  add(files: readonly File[]): void {
    for (const file of files) {
      const item: UploadItem = { id: this.nextId++, fileName: file.name, percent: 0, state: 'uploading', error: null, file };
      const refusal = refuse(file);
      if (refusal) {
        this.uploads.update((u) => [...u, { ...item, state: 'failed', error: refusal }]);
        continue;
      }
      this.uploads.update((u) => [...u, item]);
      this.start(item);
    }
  }

  retry(id: number): void {
    const item = this.uploads().find((u) => u.id === id);
    if (!item || item.state !== 'failed' || refuse(item.file)) return;
    this.patch(id, { state: 'uploading', percent: 0, error: null });
    this.start(item);
  }

  /** Aborts a running upload (the browser drops the request) and removes it from the list. */
  cancel(id: number): void {
    this.running.get(id)?.unsubscribe();
    this.running.delete(id);
    this.uploads.update((u) => u.filter((x) => x.id !== id));
  }

  dismiss(id: number): void {
    this.uploads.update((u) => u.filter((x) => x.id !== id));
  }

  private start(item: UploadItem): void {
    const flightId = this.flightId;
    if (!flightId) return;
    const sub = this.api.upload(flightId, this.section, item.file).subscribe({
      next: (e) => {
        if (e.kind === 'progress') return this.patch(item.id, { percent: e.percent });
        this.running.delete(item.id);
        this.uploads.update((u) => u.filter((x) => x.id !== item.id));
        if (this.flightId === flightId) this.images.update((all) => [...all, e.image].sort((a, b) => a.order - b.order));
      },
      error: (e) => {
        this.running.delete(item.id);
        this.patch(item.id, { state: 'failed', error: describe(e, 'The upload failed.') });
      },
    });
    this.running.set(item.id, sub);
  }

  /** Moves an image one place up (-1) or down (+1) within the section. */
  move(imageId: string, by: -1 | 1): void {
    const flightId = this.flightId;
    const list = this.images();
    const from = list.findIndex((i) => i.id === imageId);
    const to = from + by;
    if (!flightId || from < 0 || to < 0 || to >= list.length || this.busyId()) return;

    const next = [...list];
    [next[from], next[to]] = [next[to], next[from]];
    this.busyId.set(imageId);
    this.problem.set(null);
    this.api.reorder(flightId, this.section, next.map((i) => i.id)).subscribe({
      next: () => {
        this.images.set(next.map((i, order) => ({ ...i, order })));
        this.busyId.set(null);
      },
      error: (e) => {
        this.busyId.set(null);
        this.problem.set(describe(e, 'The order could not be saved.'));
      },
    });
  }

  remove(imageId: string): void {
    const flightId = this.flightId;
    if (!flightId || this.busyId()) return;
    this.busyId.set(imageId);
    this.problem.set(null);
    this.api.remove(flightId, imageId).subscribe({
      next: () => {
        this.images.update((all) => all.filter((i) => i.id !== imageId));
        this.busyId.set(null);
      },
      error: (e) => {
        this.busyId.set(null);
        this.problem.set(describe(e, 'The image could not be removed.'));
      },
    });
  }

  cancelAll(): void {
    for (const sub of this.running.values()) sub.unsubscribe();
    this.running.clear();
  }

  private patch(id: number, change: Partial<UploadItem>): void {
    this.uploads.update((u) => u.map((x) => (x.id === id ? { ...x, ...change } : x)));
  }
}

function refuse(file: File): string | null {
  if (!(ACCEPTED_IMAGE_TYPES as readonly string[]).includes(file.type)) return 'Only JPEG, PNG and WebP images can be attached.';
  if (file.size > MAX_IMAGE_BYTES) return `This image is larger than ${MAX_IMAGE_BYTES / (1024 * 1024)} MB.`;
  if (file.size === 0) return 'This file is empty.';
  return null;
}

function describe(e: unknown, fallback: string): string {
  if (!(e instanceof HttpErrorResponse)) return fallback;
  if (e.status === 0) return `${fallback} The server could not be reached.`;
  if (e.status === 403) return "You can't change this flight's images.";
  if (e.status === 413) return 'This image is too large.';
  if (e.status === 502) return 'The file store is unavailable right now. Try again in a moment.';
  const errors = (e.error as { errors?: Record<string, string[]> } | null)?.errors;
  const detail = errors ? Object.values(errors).flat().join(' ') : '';
  return detail || fallback;
}
