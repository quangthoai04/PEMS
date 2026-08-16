/**
 * i18n gate — Phase 14 (notification event-type coverage).
 *
 * Every eventKey the frontend's `resolveNotificationPresentation` knows how to render (the same
 * set the backend's `NotificationEventKeys` C# class defines — see that file's header comment for
 * why the two are a hand-kept-in-sync pair rather than one generated from the other) must have a
 * complete VI + EN title, and — for every key except the two zero-param amendment-decision
 * events — a message too, plus every `{{param}}` placeholder actually used.
 *
 * `localeParity.test.ts` already proves VI/EN have identical key sets project-wide (so it already
 * catches an event with EN but no VI, or vice versa); this test is the complementary, narrower
 * check: does each event's key/param CONTRACT make sense on its own (title present, message
 * present unless deliberately title-only, params list is non-empty where the backend actually
 * sends params)? Also guards the unmapped-event fallback (`events.unknown.title`) exists in both
 * languages, since that's what a legacy/unbackfilled notification renders in EN — see
 * resolveNotificationPresentation.ts.
 */
import { describe, expect, it } from 'vitest';
import en from '../../shared/i18n/locales/en/notifications.json';
import vi from '../../shared/i18n/locales/vi/notifications.json';

/** eventKey -> the params it is built with, one call site per key (see the 7 producer files under
 * backend/PEMS.Application/.../Commands/* and Services/CampusApprovalExecutor.cs). Kept here as a
 * plain literal list rather than importing C# — this is the frontend's OWN understanding of the
 * contract, which is exactly what should be asserted (a backend/frontend drift is what this test
 * exists to catch, not something to paper over by importing one side's definition into the other). */
const EVENT_PARAMS: Record<string, string[]> = {
  CAMPUS_APPROVED: ['campusName', 'requestCode', 'hostName'],
  CAMPUS_REJECTED: ['campusName', 'requestCode', 'reason'],
  FEEDBACK_INVITE_VISITOR: ['requestCode'],
  VISIT_CLOSED: ['requestCode'],
  VISIT_CANCELLED_BY_HOST: ['campusName', 'requestCode'],
  OPCONTACT_TRANSFER_FROM: ['campusLabel', 'requestCode'],
  OPCONTACT_TRANSFER_TO: ['campusLabel', 'requestCode'],
  AMENDMENT_APPROVED: [],
  AMENDMENT_REJECTED: [],
  HOST_CHANGED: ['campusName', 'requestCode', 'hostName'],
  ACCOUNT_CREATED: [],
  ACCOUNT_LOCKED: [],
  ACCOUNT_UNLOCKED: [],
};

type EventEntry = { title: string; message?: string };
const events = { en: en.events as Record<string, EventEntry>, vi: vi.events as Record<string, EventEntry> };

describe('Notification event-type coverage (Phase 14 gate)', () => {
  it('the unmapped-event fallback title exists in both languages', () => {
    expect(events.en.unknown?.title, 'notifications:events.unknown.title missing in EN').toBeTruthy();
    expect(events.vi.unknown?.title, 'notifications:events.unknown.title missing in VI').toBeTruthy();
  });

  for (const [eventKey, params] of Object.entries(EVENT_PARAMS)) {
    describe(eventKey, () => {
      it('has a non-empty title in both languages', () => {
        expect(events.en[eventKey]?.title, `EN title missing for ${eventKey}`).toBeTruthy();
        expect(events.vi[eventKey]?.title, `VI title missing for ${eventKey}`).toBeTruthy();
      });

      it('has a non-empty message in both languages', () => {
        expect(events.en[eventKey]?.message, `EN message missing for ${eventKey}`).toBeTruthy();
        expect(events.vi[eventKey]?.message, `VI message missing for ${eventKey}`).toBeTruthy();
      });

      it('the title+message together reference every declared param at least once', () => {
        const enText = `${events.en[eventKey]?.title ?? ''} ${events.en[eventKey]?.message ?? ''}`;
        const viText = `${events.vi[eventKey]?.title ?? ''} ${events.vi[eventKey]?.message ?? ''}`;
        for (const param of params) {
          const placeholder = `{{${param}}}`;
          expect(enText.includes(placeholder), `EN text for ${eventKey} never uses ${placeholder}`).toBe(true);
          expect(viText.includes(placeholder), `VI text for ${eventKey} never uses ${placeholder}`).toBe(true);
        }
      });

      it('never references a param it did not declare', () => {
        const enText = `${events.en[eventKey]?.title ?? ''} ${events.en[eventKey]?.message ?? ''}`;
        const declared = new Set(params);
        for (const m of enText.matchAll(/\{\{(\w+)\}\}/g)) {
          expect(declared.has(m[1]), `EN text for ${eventKey} references undeclared param {{${m[1]}}}`).toBe(true);
        }
      });
    });
  }

  it('KNOWN_EVENT_KEYS in resolveNotificationPresentation.ts matches this test\'s event list exactly', async () => {
    const mod = await import('fs');
    const path = await import('path');
    const src = mod.readFileSync(
      path.join(__dirname, '../../features/notifications/utils/resolveNotificationPresentation.ts'),
      'utf-8',
    );
    const match = src.match(/KNOWN_EVENT_KEYS = new Set\(\[([\s\S]*?)\]\)/);
    expect(match, 'Could not find KNOWN_EVENT_KEYS in resolveNotificationPresentation.ts — update this test if it was renamed.').toBeTruthy();
    const declaredKeys = [...(match![1].matchAll(/'([A-Z_]+)'/g))].map((m) => m[1]).sort();
    expect(declaredKeys).toEqual(Object.keys(EVENT_PARAMS).sort());
  });
});
