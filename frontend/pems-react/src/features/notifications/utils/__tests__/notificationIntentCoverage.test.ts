/**
 * Automated coverage guard (plan-continuation §22/§23): for every LIVE eventKey the backend can
 * actually emit, this asserts —
 *
 *   1. the presentation layer recognizes it (KNOWN_EVENT_KEYS, resolveNotificationPresentation.ts);
 *   2. a VI translation exists (events.{key}.title + .message);
 *   3. an EN translation exists (same);
 *   4. a navigation intent is classified (classifyNotificationIntent, resolveNotificationDestination.ts).
 *
 * "Live" is read straight from `NotificationEventKeys.cs` at test time — not a hand-maintained list
 * — so a developer adding a new eventKey constant without wiring routing/locale for it fails THIS
 * test immediately, per plan §22: "Nếu thêm eventKey mới mà quên routing/locale: test FAIL."
 *
 * The reverse direction is checked too: every key the frontend claims to know (KNOWN_EVENT_KEYS)
 * must correspond to a real backend constant — an orphaned frontend entry is dead weight nobody
 * will ever notice drifted.
 *
 * Distinct from `src/test/i18n/notificationEventCoverage.test.ts` (the pre-existing "Phase 14
 * gate", found already in place at the start of this pass): that one owns per-event PARAM-usage
 * correctness (title/message reference every declared `{{param}}` and no undeclared one) off a
 * hand-kept list. This file's unique job is the NAVIGATION INTENT dimension that gate does not
 * check at all, plus reading the live key set directly from the backend source rather than a second
 * hand-kept list — the two are complementary, not redundant, and both must stay green.
 */
import fs from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import { KNOWN_EVENT_KEYS } from '../resolveNotificationPresentation';
import { classifyNotificationIntent } from '../resolveNotificationDestination';

const REPO_ROOT = path.resolve(__dirname, '../../../../../../..');
const SRC_ROOT = path.resolve(__dirname, '../../../../');

const EVENT_KEYS_CS = path.join(
  REPO_ROOT,
  'backend/PEMS.Application/Notifications/Common/NotificationEventKeys.cs',
);

/**
 * Extracts every `public const string Name = "VALUE";` from NotificationEventKeys.cs — the
 * class's ONLY members besides the `BuildMetadata` helper and its private record, both of which
 * this pattern naturally excludes (they aren't `const string` declarations).
 */
function readLiveEventKeys(): string[] {
  const src = fs.readFileSync(EVENT_KEYS_CS, 'utf-8');
  const pattern = /public const string \w+\s*=\s*"([A-Z0-9_]+)";/g;
  const keys: string[] = [];
  let match: RegExpExecArray | null;
  while ((match = pattern.exec(src)) !== null) {
    keys.push(match[1]);
  }
  return keys;
}

function readLocaleEvents(lang: 'vi' | 'en'): Record<string, { title?: string; message?: string }> {
  const file = path.join(SRC_ROOT, `shared/i18n/locales/${lang}/notifications.json`);
  const json = JSON.parse(fs.readFileSync(file, 'utf-8'));
  return json.events ?? {};
}

describe('notification eventKey coverage guard', () => {
  const liveKeys = readLiveEventKeys();
  const viEvents = readLocaleEvents('vi');
  const enEvents = readLocaleEvents('en');

  it('found a non-trivial live eventKey list (sanity check the file was actually read)', () => {
    expect(liveKeys.length).toBeGreaterThanOrEqual(50);
  });

  it.each(liveKeys)('%s is recognized by the presentation parser (KNOWN_EVENT_KEYS)', (key) => {
    expect(KNOWN_EVENT_KEYS.has(key)).toBe(true);
  });

  it.each(liveKeys)('%s has a VI title + message', (key) => {
    expect(viEvents[key]?.title, `vi/notifications.json events.${key}.title`).toBeTruthy();
    expect(viEvents[key]?.message, `vi/notifications.json events.${key}.message`).toBeTruthy();
  });

  it.each(liveKeys)('%s has an EN title + message', (key) => {
    expect(enEvents[key]?.title, `en/notifications.json events.${key}.title`).toBeTruthy();
    expect(enEvents[key]?.message, `en/notifications.json events.${key}.message`).toBeTruthy();
  });

  it.each(liveKeys)('%s has a navigation intent classification', (key) => {
    const intent = classifyNotificationIntent({
      metadataJson: JSON.stringify({ eventKey: key, params: {} }),
      actionType: null,
    });
    // No eventKey may resolve to `null` here — an event this codebase actually creates rows for
    // must always have an explicit destination class, even if that class is a passive one like
    // VISIT_READONLY_DETAIL. `null` is reserved for legacy rows with NO recognizable eventKey at
    // all, never for a live one that was simply forgotten.
    expect(intent, `classifyNotificationIntent has no entry for live eventKey ${key}`).not.toBeNull();
  });

  it('every frontend-known eventKey corresponds to a real backend constant (no orphans)', () => {
    const liveSet = new Set(liveKeys);
    const orphans = [...KNOWN_EVENT_KEYS].filter((k) => !liveSet.has(k));
    expect(orphans).toEqual([]);
  });
});
