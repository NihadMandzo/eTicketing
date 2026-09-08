import { safeReturnUrl } from './return-url.util';

/**
 * The interesting cases here are the attacks, not the happy path — `safeReturnUrl` exists so a
 * crafted `?returnUrl=` on the login/verify-email pages cannot redirect a freshly-signed-in visitor
 * off this site (open-redirect). See the function's doc comment for the mechanics of each attack.
 */
describe('safeReturnUrl', () => {
  it('passes a same-app absolute path straight through', () => {
    expect(safeReturnUrl('/dogadjaji/abc-123')).toBe('/dogadjaji/abc-123');
  });

  it('keeps the path together with its own query string', () => {
    expect(safeReturnUrl('/dogadjaji/abc-123?tab=sektori')).toBe('/dogadjaji/abc-123?tab=sektori');
  });

  it('allows hyphenated route segments', () => {
    expect(safeReturnUrl('/zaboravljena-lozinka')).toBe('/zaboravljena-lozinka');
  });

  it('falls back for null/undefined/empty input', () => {
    expect(safeReturnUrl(null)).toBe('/');
    expect(safeReturnUrl(undefined)).toBe('/');
    expect(safeReturnUrl('')).toBe('/');
  });

  it('falls back for a path that does not start with "/"', () => {
    expect(safeReturnUrl('dogadjaji/123')).toBe('/');
  });

  it('rejects a protocol-relative URL ("//host") as the open-redirect it is', () => {
    expect(safeReturnUrl('//evil.example/login')).toBe('/');
    expect(safeReturnUrl('//evil.example')).toBe('/');
  });

  it('rejects a backslash-prefixed URL some parsers also treat as protocol-relative', () => {
    expect(safeReturnUrl('/\\evil.example')).toBe('/');
  });

  it('rejects a full external URL outright', () => {
    expect(safeReturnUrl('https://evil.example')).toBe('/');
    expect(safeReturnUrl('http://evil.example/prijava')).toBe('/');
  });

  it('rejects a value containing a control character', () => {
    expect(safeReturnUrl('/dogadjaji/\t123')).toBe('/');
    expect(safeReturnUrl('/dogadjaji/\n123')).toBe('/');
  });

  // A plain space carries no origin-changing power — unlike a backslash or a control character, it
  // cannot smuggle a host past the "/"-prefix check, so it is left alone rather than over-rejected.
  it('allows an ordinary space in the path', () => {
    expect(safeReturnUrl('/dogadjaji/some event')).toBe('/dogadjaji/some event');
  });

  it('honors a custom fallback', () => {
    expect(safeReturnUrl('//evil.example', '/dogadjaji')).toBe('/dogadjaji');
  });
});
