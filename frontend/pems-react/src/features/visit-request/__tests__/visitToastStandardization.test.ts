import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

/**
 * Structural guard for the one thing a rendering test cannot see: that these screens no longer ship
 * their OWN toast machinery.
 *
 * The app mounts exactly one <Toaster position="top-right"> in App.tsx. VisitRequestManagement used to
 * run a private viewport pinned to `fixed bottom-5 right-5`, which is why cancelling a visit produced
 * a notification in the opposite corner from every other action in the product. Asserting on the
 * source is deliberate: the defect is the EXISTENCE of a second system, and a component test that
 * renders one screen can never prove absence across the flow.
 */
const read = (relative: string) =>
  readFileSync(resolve(__dirname, '..', '..', '..', relative), 'utf8');

const MIGRATED = [
  'pages/dashboard/visit/VisitRequestManagement.tsx',
  'features/visit-request/components/ContactIdentityActions.tsx',
  'features/visit-request/components/VisitAmendmentPanel.tsx',
  'features/visit-request/components/VisitSafeEditModal.tsx',
  'features/visit-request/components/VisitAmendmentSubmitModal.tsx',
];

describe('visit mutation feedback is standardized on the global toaster', () => {
  it.each(MIGRATED)('%s mounts no toast viewport of its own', file => {
    const source = read(file);
    expect(source).not.toMatch(/fixed\s+bottom-\d+\s+right-\d+/);
    expect(source).not.toMatch(/<Toaster/);
  });

  it.each(MIGRATED)('%s has no local pushToast implementation', file => {
    const source = read(file);
    expect(source).not.toMatch(/const pushToast\s*=/);
    expect(source).not.toMatch(/type Toast\s*=\s*\{/);
  });

  it.each(MIGRATED)('%s reports outcomes through the shared helper', file => {
    const source = read(file);
    expect(source).toMatch(/from '.*shared\/utils\/toast'/);
  });

  it('the app still mounts exactly one Toaster', () => {
    const app = read('App.tsx');
    expect(app.match(/<Toaster/g) ?? []).toHaveLength(1);
    expect(app).toMatch(/position="top-right"/);
  });
});
