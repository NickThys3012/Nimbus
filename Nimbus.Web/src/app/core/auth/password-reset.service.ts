import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { OpenAPI } from '../api-client';

// Not in the generated client yet; swap for it after the next `generate:api`.
@Injectable({ providedIn: 'root' })
export class PasswordResetService {
  private readonly http = inject(HttpClient);

  forgotPassword(email: string): Observable<void> {
    return this.http.post<void>(`${OpenAPI.BASE}/api/authentication/forgot-password`, { email });
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${OpenAPI.BASE}/api/authentication/reset-password`, {
      email,
      token,
      newPassword,
    });
  }
}
