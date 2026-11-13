import { HttpEventType } from '@angular/common/http';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { FlightImagesApi, type UploadEvent } from './flight-images.api';

describe('FlightImagesApi (issue #50)', () => {
  let api: FlightImagesApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting()] });
    api = TestBed.inject(FlightImagesApi);
    http = TestBed.inject(HttpTestingController);
  });

  it('posts the file and section as multipart and reports progress as a percentage, then the stored image', () => {
    const events: UploadEvent[] = [];
    api.upload('f1', 'Meteo', new File(['x'], 'stuve.png', { type: 'image/png' })).subscribe((e) => events.push(e));

    const req = http.expectOne((r) => r.url.endsWith('/api/flight-preparations/f1/images'));
    expect(req.request.method).toBe('POST');
    const body = req.request.body as FormData;
    expect(body.get('section')).toBe('Meteo');
    expect((body.get('file') as File).name).toBe('stuve.png');

    req.event({ type: HttpEventType.UploadProgress, loaded: 25, total: 100 });
    const stored = { id: 'n', section: 'Meteo', contentType: 'image/png', url: 'u', thumbnailUrl: 't', sizeBytes: 1, order: 0 };
    req.flush(stored);

    expect(events).toEqual([{ kind: 'progress', percent: 25 }, { kind: 'done', image: stored }]);
  });

  it('aborts the request when the subscriber unsubscribes (that is how an upload is cancelled)', () => {
    const sub = api.upload('f1', 'Meteo', new File(['x'], 'a.png', { type: 'image/png' })).subscribe();
    const req = http.expectOne((r) => r.url.endsWith('/images'));

    sub.unsubscribe();

    expect(req.cancelled).toBe(true);
  });

  it('sends the full new order of a section', () => {
    api.reorder('f1', 'Meteo', ['b', 'a']).subscribe();
    const req = http.expectOne((r) => r.url.endsWith('/api/flight-preparations/f1/images/order'));
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ section: 'Meteo', imageIds: ['b', 'a'] });
    req.flush(null);
  });

  it('deletes one image', () => {
    api.remove('f1', 'img1').subscribe();
    const req = http.expectOne((r) => r.url.endsWith('/api/flight-preparations/f1/images/img1'));
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
