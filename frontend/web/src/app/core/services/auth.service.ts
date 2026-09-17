import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, finalize, map, Observable, of, shareReplay, tap } from 'rxjs';

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

  /** The single `/auth/refresh` currently in flight, shared by every caller that needs it — see
   * `refresh()` for why concurrent refreshes must not each issue their own request. */
  private refreshInFlight: Observable<UserResponse> | null = null;

  /** Bumped by `logout()`. A refresh remembers the value it started under, and only a refresh
   * that finishes under the same value may set `currentUser`. */
  private refreshEpoch = 0;

  login(request: LoginRequest): Observable<UserResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/login`, request).pipe(
      map((response) => response.user),
      tap((user) => this.currentUser.set(user)),
    );
  }

  register(request: RegisterRequest): Observable<UserResponse> {
    return this.http.post<LoginResponse>(`${this.baseUrl}/register`, request).pipe(
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

  /**
   * Silently renews the session using the refresh cookie — used by the auth-refresh interceptor
   * on a 401.
   *
   * Concurrent callers share one in-flight request, and that sharing is mandatory rather than an
   * optimization. The backend rotates the refresh token under an atomic compare-and-swap
   * (`AuthService.RefreshAsync` → `TryRevokeAsync`): of N requests presenting the same token,
   * exactly one flips `RevokedAt` and is issued a successor, and every other one is rejected by
   * design, because a second valid successor is precisely what token rotation exists to prevent.
   *
   * A page load fires several requests at once (`/auth/me` from the app initializer, plus
   * whatever the landing component requests), so when the short-lived `eticketing_at` cookie has
   * expired they all 401 together. Firing one `/auth/refresh` per 401 therefore sent N racing
   * rotations at a backend guaranteed to fail all but one — and each loser's `catchError` in the
   * interceptor cleared `currentUser`, wiping a session that had just been renewed successfully.
   * Whether the visitor ended up signed in came down to which callback landed last. That is the
   * "I'm signed out, but I'm signed in again once I try to pay" symptom: an isolated later
   * request refreshes alone, wins uncontested, and restores the state the stampede had discarded.
   *
   * Worse, a loser presenting an already-rotated token is indistinguishable from a replayed
   * stolen one, so it can trip reuse detection and revoke the whole token family — turning a
   * routine 15-minute expiry into a forced re-login.
   *
   * `finalize` clears the shared handle once the request settles, so the next expiry starts a new
   * refresh rather than replaying this one's result — but only if the handle is still this
   * refresh's. After a `logout()` a newer refresh may have taken the slot, and an older one
   * settling late must not drop it, or concurrent 401s would start racing rotations again.
   */
  refresh(): Observable<UserResponse> {
    if (this.refreshInFlight) {
      return this.refreshInFlight;
    }

    const epoch = this.refreshEpoch;
    const refresh$: Observable<UserResponse> = this.http
      .post<LoginResponse>(`${this.baseUrl}/refresh`, {})
      .pipe(
        map((response) => response.user),
        tap((user) => {
          // A logout since this refresh started has already cleared the session; this response
          // belongs to the session it ended and must not sign the visitor back in.
          if (epoch === this.refreshEpoch) {
            this.currentUser.set(user);
          }
        }),
        finalize(() => {
          if (this.refreshInFlight === refresh$) {
            this.refreshInFlight = null;
          }
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    this.refreshInFlight = refresh$;
    return refresh$;
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
    // A refresh already in flight cannot be cancelled by dropping the handle: the interceptor that
    // started it is still subscribed, so its response still arrives. Moving the epoch is what
    // stops that response from repopulating `currentUser` after we cleared it — which would leave
    // the app looking signed in against a session the server has already revoked. Dropping the
    // handle as well keeps a request made after logout from joining that stale refresh.
    this.refreshEpoch++;
    this.refreshInFlight = null;

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
