import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { provideRouter } from '@angular/router';

import { UserResponse } from '../models/auth.models';
import { AuthService } from '../services/auth.service';
import { authGuard } from './auth.guard';

const USER = { id: 'u1', firstName: 'Nihad', username: 'nihad' } as UserResponse;

/** Runs the guard the way the router does, under a chosen platform. */
function runGuard(platform: 'browser' | 'server', url: string, user: UserResponse | null) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: PLATFORM_ID, useValue: platform }],
  });

  TestBed.inject(AuthService).currentUser.set(user);

  return TestBed.runInInjectionContext(() =>
    authGuard({} as never, { url } as RouterStateSnapshot),
  );
}

describe('authGuard', () => {
  it('lets a signed-in visitor through', () => {
    expect(runGuard('browser', '/placanje', USER)).toBe(true);
  });

  it('sends a signed-out visitor to the login page, carrying where they were going', () => {
    const result = runGuard('browser', '/placanje', null);

    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe(
      '/prijava?returnUrl=%2Fplacanje',
    );
  });

  it('preserves the attempted URL query string in returnUrl', () => {
    const result = runGuard('browser', '/placanje?sectorId=abc', null);

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe(
      '/prijava?returnUrl=%2Fplacanje%3FsectorId%3Dabc',
    );
  });

  it('never redirects on the server, where the session cookie is unreachable', () => {
    // The regression this guards: auth state is restored browser-only, so on the server
    // `currentUser` is null for *everyone*, signed in or not. Redirecting on that basis sent
    // signed-in users to the login page — and under prerendering it baked that redirect into a
    // static file, so every visitor hit it forever.
    expect(runGuard('server', '/placanje', null)).toBe(true);
    expect(runGuard('server', '/profil', null)).toBe(true);
  });
});
