import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

/**
 * The path the backend puts in the operational-contact invitation email.
 *
 * Built by `OperationalContactInvitationService.SendInvitationAsync` as
 * `{App:FrontendBaseUrl}/operational-contact-confirmation/{rawToken}`. It is the ONLY way an invited
 * guest-side contact reaches the confirmation, and therefore the only way the request-level gate ever
 * opens — a request whose link lands on a route that does not exist can never leave
 * PENDING_CONTACT_CONFIRMATION.
 *
 * This lived through a whole cutover as a silent break: the sender moved to this path while the router
 * still only carried the two addresses the removed request-level flow had used, so every invitation
 * pointed at the SPA's not-found. Nothing failed loudly, because nothing on either side knows about
 * the other.
 */
const EMAILED_PATH = '/operational-contact-confirmation/:token';

/** Addresses the request-level flow used to send. Links already in an inbox outlive the code. */
const LEGACY_PATHS = ['/visit-contact-claim/:token', '/visit-contact-transfer/:token'];

describe('operational-contact invitation route', () => {
  // Resolved from the vitest root rather than from import.meta.url, which is not a file URL here.
  // Asserted to exist first, so a moved App.tsx fails as "the file is gone" instead of as a
  // missing route.
  const appPath = resolve(process.cwd(), 'src/App.tsx');
  it('can see the route table', () => expect(existsSync(appPath)).toBe(true));
  const app = existsSync(appPath) ? readFileSync(appPath, 'utf8') : '';

  it('routes the path the invitation email actually points at', () => {
    expect(app).toContain(`path="${EMAILED_PATH}"`);
  });

  it('still answers the addresses older invitations were sent to', () => {
    for (const path of LEGACY_PATHS) expect(app).toContain(`path="${path}"`);
  });

  it('sends all three to the same landing page', () => {
    for (const path of [EMAILED_PATH, ...LEGACY_PATHS]) {
      const route = app.slice(app.indexOf(`path="${path}"`));
      expect(route.slice(0, 200)).toContain('VisitContactInvitationPage');
    }
  });
});
