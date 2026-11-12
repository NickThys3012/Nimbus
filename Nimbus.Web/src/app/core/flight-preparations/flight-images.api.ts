import { HttpClient, HttpEventType } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { filter, map, type Observable } from 'rxjs';

import { type FlightImageDto, OpenAPI } from '../api-client';

/** What an upload reports while it runs: a percentage, then the stored image. */
export type UploadEvent = { kind: 'progress'; percent: number } | { kind: 'done'; image: FlightImageDto };

/**
 * The image endpoints of a flight (issue #51). Hand-written because the generated client cannot report upload progress;
 * unsubscribing from `upload` aborts the request, which is how an upload is cancelled.
 */
@Injectable({ providedIn: 'root' })
export class FlightImagesApi {
  private readonly http = inject(HttpClient);

  private url(flightId: string, tail = ''): string {
    return `${OpenAPI.BASE}/api/flight-preparations/${flightId}/images${tail}`;
  }

  list(flightId: string): Observable<FlightImageDto[]> {
    return this.http.get<FlightImageDto[]>(this.url(flightId));
  }

  upload(flightId: string, section: string, file: File): Observable<UploadEvent> {
    const body = new FormData();
    body.append('section', section);
    body.append('file', file, file.name);
    return this.http.post<FlightImageDto>(this.url(flightId), body, { observe: 'events', reportProgress: true }).pipe(
      filter((e) => e.type === HttpEventType.UploadProgress || e.type === HttpEventType.Response),
      map((e): UploadEvent => {
        if (e.type === HttpEventType.Response) return { kind: 'done', image: e.body as FlightImageDto };
        const total = e.type === HttpEventType.UploadProgress ? e.total : undefined;
        return { kind: 'progress', percent: total ? Math.min(100, Math.round((e.loaded / total) * 100)) : 0 };
      }),
    );
  }

  /** `imageIds` is the whole new order of the section, first to last. */
  reorder(flightId: string, section: string, imageIds: string[]): Observable<void> {
    return this.http.put<void>(this.url(flightId, '/order'), { section, imageIds });
  }

  remove(flightId: string, imageId: string): Observable<void> {
    return this.http.delete<void>(this.url(flightId, `/${imageId}`));
  }
}
