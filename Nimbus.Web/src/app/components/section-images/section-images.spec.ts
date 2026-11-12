import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import { vi } from 'vitest';

import type { FlightImageDto } from '../../core/api-client';
import { FlightImagesApi, type UploadEvent } from '../../core/flight-preparations/flight-images.api';
import { SectionImages } from './section-images';

const image = (id: string, order: number): FlightImageDto => ({
  id, section: 'Meteo', order, contentType: 'image/png', url: `https://f.test/${id}`, thumbnailUrl: `https://f.test/${id}.thumb`, sizeBytes: 3 * 1024 * 1024, uploadedAt: null,
});

@Component({ imports: [SectionImages], template: `<Nimbus-section-images section="Meteo" [flightId]="flightId()" />` })
class Host {
  flightId = signal<string | null>('f1');
}

describe('SectionImages (issue #50)', () => {
  const api = { list: vi.fn(), upload: vi.fn(), reorder: vi.fn(), remove: vi.fn() };

  async function setup(flightId: string | null = 'f1') {
    api.list.mockReturnValue(of([image('a', 0), image('b', 1)]));
    api.reorder.mockReturnValue(of(undefined));
    api.remove.mockReturnValue(of(undefined));
    await TestBed.configureTestingModule({ providers: [{ provide: FlightImagesApi, useValue: api }] }).compileComponents();
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.flightId.set(flightId);
    const flush = async () => {
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    await flush();
    const el = fixture.nativeElement as HTMLElement;
    return { el, flush, q: (id: string) => el.querySelector(`[data-testid="${id}"]`) as HTMLElement | null, all: (id: string) => Array.from(el.querySelectorAll(`[data-testid="${id}"]`)) as HTMLElement[] };
  }

  beforeEach(() => vi.clearAllMocks());

  it('asks to save the flight first, and offers no upload, when the flight has no id yet', async () => {
    const { q } = await setup(null);
    expect(q('images-unsaved')?.textContent).toContain('Save the flight first');
    expect(q('add-image')).toBeNull();
  });

  it('lists the section images as thumbnails linking to the original, with size and position', async () => {
    const { el, all } = await setup();
    const imgs = Array.from(el.querySelectorAll('img'));
    expect(imgs.map((i) => i.getAttribute('src'))).toEqual(['https://f.test/a.thumb', 'https://f.test/b.thumb']);
    expect(el.querySelector('a.thumb')?.getAttribute('href')).toBe('https://f.test/a');
    expect(el.textContent).toContain('3.0 MB');
    // First cannot move up, last cannot move down.
    expect((all('image-up')[0] as HTMLButtonElement).disabled).toBe(true);
    expect((all('image-down')[1] as HTMLButtonElement).disabled).toBe(true);
  });

  it('shows a progress bar and a cancel button for a running upload, and drops it on cancel', async () => {
    const run = new Subject<UploadEvent>();
    api.upload.mockReturnValue(run);
    const { el, q, flush } = await setup();

    const input = q('image-file') as HTMLInputElement;
    const file = new File([new Uint8Array(10)], 'sounding.png', { type: 'image/png' });
    Object.defineProperty(input, 'files', { value: [file], configurable: true });
    input.dispatchEvent(new Event('change'));
    run.next({ kind: 'progress', percent: 60 });
    await flush();

    expect(el.textContent).toContain('sounding.png');
    expect(q('upload-percent')?.textContent).toBe('60%');
    expect(el.querySelector('[role="progressbar"]')?.getAttribute('aria-valuenow')).toBe('60');

    q('upload-cancel')?.click();
    await flush();
    expect(el.querySelector('[role="progressbar"]')).toBeNull();
    expect(run.observed).toBe(false);
  });

  it('shows a failed upload as an alert with Retry and Dismiss', async () => {
    const { q, flush } = await setup();
    const input = q('image-file') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: [new File(['x'], 'notes.pdf', { type: 'application/pdf' })], configurable: true });
    input.dispatchEvent(new Event('change'));
    await flush();

    expect(q('upload-error')?.textContent).toContain('Only JPEG, PNG and WebP');
    expect(q('upload-retry')).not.toBeNull();
    q('upload-dismiss')?.click();
    await flush();
    expect(q('upload-error')).toBeNull();
  });

  it('reorders and removes through the buttons', async () => {
    const { all, flush, el } = await setup();

    all('image-down')[0].click();
    await flush();
    expect(api.reorder).toHaveBeenCalledWith('f1', 'Meteo', ['b', 'a']);

    all('image-remove')[0].click();
    await flush();
    expect(api.remove).toHaveBeenCalledWith('f1', 'b');
    expect(el.querySelectorAll('img').length).toBe(1);
  });
});
