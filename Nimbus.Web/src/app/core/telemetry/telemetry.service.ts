import { Injectable, ErrorHandler, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { OpenAPI } from '../api-client';

export type ClientTelemetryEventType = 'PageView' | 'UnhandledError';

export interface ClientTelemetryEvent {
  type: ClientTelemetryEventType;
  url: string;
  message?: string;
  stack?: string;
}

/**
 * Reports page views and unhandled errors to `POST /api/telemetry` (issue #12),
 * so they land in the same Loki/Grafana pipeline as backend logs. Best-effort:
 * a failure to report telemetry must never surface to the user or break
 * navigation/error handling, so every send swallows its own errors.
 */
@Injectable({ providedIn: 'root' })
export class TelemetryService {
  private readonly http = inject(HttpClient);

  reportPageView(url: string): void {
    this.send({ type: 'PageView', url });
  }

  reportError(url: string, message: string, stack?: string): void {
    this.send({ type: 'UnhandledError', url, message, stack });
  }

  private send(event: ClientTelemetryEvent): void {
    this.http.post(`${OpenAPI.BASE}/api/telemetry`, [event]).subscribe({
      error: () => {
        // Deliberately swallowed: telemetry reporting must never itself throw
        // an unhandled error, and there is nowhere else to report this failure to.
      },
    });
  }
}

/**
 * Forwards uncaught Angular errors to the telemetry pipeline before falling
 * back to the default console logging behaviour.
 */
@Injectable()
export class TelemetryErrorHandler implements ErrorHandler {
  private readonly telemetry = inject(TelemetryService);

  handleError(error: unknown): void {
    const err = error instanceof Error ? error : new Error(String(error));
    this.telemetry.reportError(
      typeof window !== 'undefined' ? window.location.pathname : '',
      err.message,
      err.stack,
    );
    // eslint-disable-next-line no-console
    console.error(error);
  }
}
