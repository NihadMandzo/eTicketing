import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, map, Observable, of, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  ChangePasswordRequest,
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  ResetPasswordRequest,
  UpdateUserRequest,
  UserResponse,
  VerifyEmailRequest,
} from '../models/auth.models';

/**
 * All auth state lives server-side in the httpOnly session cookie — this
 * service never reads or stores a token itself. The credentials interceptor
 * attaches the cookie to every request; the backend attaches/receives it via
 * `Set-Cookie` on login/register/refresh/logout.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/auth`;

  readonly currentUser = signal<UserResponse | null>(null);
  readonly isAuthenticated = computed(() => this.currentUser() !== null);

  login(request: LoginRequest): Observable<UserResponse> {
    return this.http
      .post<LoginResponse>(`${this.baseUrl}/login`, request)
      .pipe(
        map((response) => response.user),
        tap((user) => this.currentUser.set(user)),
      );
  }

  register(request: RegisterRequest): Observable<UserResponse> {
    return this.http
      .post<LoginResponse>(`${this.baseUrl}/register`, request)
      .pipe(
        map((response) => response.user),
        tap((user) => this.currentUser.set(user)),
      );
  }

  /** Restores auth state from the session cookie — called once on app init
   * so a page refresh doesn't lose the signed-in state. */
  loadCurrentUser(): Observable<UserResponse | null> {
    return this.http.get<UserResponse>(`${this.baseUrl}/me`).pipe(
      tap((user) => this.currentUser.set(user)),
      catchError(() => {
        this.currentUser.set(null);
        return of(null);
      }),
    );
  }

  /** Silently renews the session using the refresh cookie — used by the
   * auth-refresh interceptor on a 401. */
  refresh(): Observable<UserResponse> {
    return this.http
      .post<LoginResponse>(`${this.baseUrl}/refresh`, {})
      .pipe(
        map((response) => response.user),
        tap((user) => this.currentUser.set(user)),
      );
  }

  changePassword(request: ChangePasswordRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/change-password`, request);
  }

  updateUser(request: UpdateUserRequest): Observable<UserResponse> {
    return this.http
      .put<UserResponse>(`${this.baseUrl}/update-user`, request)
      .pipe(tap((user) => this.currentUser.set(user)));
  }

  /** Confirms the 6-char code emailed on registration — requires the caller to already be
   * authenticated (Register itself signs the buyer in), so the target is always the caller's
   * own account, never one supplied in the request. */
  verifyEmail(request: VerifyEmailRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/verify-email`, request);
  }

  resendVerificationEmail(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/resend-verification-email`, {});
  }

  /** Always resolves — the backend returns 200 for both "email sent" and "no such account"
   * (anti-enumeration), so the component shows the same generic message either way. The one
   * exception (a staff/org account) surfaces as a real error with a specific message. */
  forgotPassword(request: ForgotPasswordRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/forgot-password`, request);
  }

  resetPassword(request: ResetPasswordRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/reset-password`, request);
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/logout`, {}).pipe(
      tap(() => this.currentUser.set(null)),
      catchError(() => {
        // Best-effort — clear local state even if the call itself fails.
        this.currentUser.set(null);
        return of(void 0);
      }),
    );
  }
}
