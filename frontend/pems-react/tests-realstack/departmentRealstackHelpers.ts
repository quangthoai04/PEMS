/**
 * Shared helpers for the DEPARTMENT real-stack journeys (personnel management + logistics
 * proposal/handover), added to close the two coverage gaps the Dev → Cảnh-Iter1 merge left behind.
 *
 * Same contract as `realstackHelpers.ts`: real Chromium → real React → real .NET API → the disposable
 * `pems_e2e_realstack` database → the Testing-only FileSink inbox. NO network mocking. The action under
 * test is always driven through the DOM; the authenticated API and a read-only SQL query are used for
 * preconditions and for verifying the state the action produced.
 */
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { expect, type APIRequestContext } from '@playwright/test';
import { API_BASE, hdr } from './realstackHelpers';

// ── Seeded identities (see scripts/run-realstack-e2e.mjs → writeAuthProfiles) ──────────────────

/** Phòng Đào tạo HN — department 2. Used by the personnel journeys. */
export const DEPT_TRAINING = {
  departmentId: 2,
  name: 'Phòng Đào tạo',
  leaderKey: 'dept_leader_hn',
  leaderUserId: 5,
  staffKey: 'dept_staff_hn',
  staffUserId: 6,
} as const;

/** Phòng Dịch vụ Cơ sở vật chất HN — department 3. Used by the logistics journeys. */
export const DEPT_FACILITIES = {
  departmentId: 3,
  name: 'Phòng Dịch vụ Cơ sở vật chất',
  leaderKey: 'facilities_leader_hn',
  leaderUserId: 17,
  staffKey: 'facilities_staff_hn',
  staffUserId: 18,
} as const;

// ── Read-only SQL against the disposable database ──────────────────────────────────────────────

const PROTECTED_DATABASES = ['pems_db', 'pems_test', 'pems_pr3_test'];
const DB = process.env.PEMS_E2E_DB ?? 'pems_e2e_realstack';

/**
 * Runs a read-only query against the disposable E2E database.
 *
 * Some properties these journeys must prove are simply not observable through the API or the DOM —
 * "only a hash of the confirmation token was stored", for instance, is a statement about a column the
 * API deliberately never returns. Asserting it from the UI alone would mean asserting the absence of
 * something nobody was going to show anyway.
 *
 * Refuses to run against a protected database, the same fail-closed check the orchestrator makes
 * before importing and again before dropping.
 */
export function queryDb(sql: string): string[][] {
  if (PROTECTED_DATABASES.includes(DB))
    throw new Error(`Refusing to query a protected database: ${DB}`);
  if (!/^\s*select\b/i.test(sql))
    throw new Error(`queryDb is read-only; refusing: ${sql.slice(0, 60)}`);

  const r = spawnSync(
    process.env.MYSQL_BIN ?? 'mysql',
    [
      `-u${process.env.MYSQL_USER ?? 'root'}`,
      `-p${process.env.MYSQL_PASSWORD ?? '123456'}`,
      `-h${process.env.MYSQL_HOST ?? 'localhost'}`,
      `-P${process.env.MYSQL_PORT ?? '3306'}`,
      '-N', '-B', '--default-character-set=utf8mb4', DB, '-e', sql,
    ],
    { encoding: 'utf8' },
  );
  if (r.status !== 0) throw new Error(`queryDb failed: ${r.stderr}`);
  return (r.stdout ?? '').trim().split('\n').filter(Boolean).map(line => line.split('\t'));
}

/** Convenience for a single scalar. Returns null when the query matched no row. */
export function scalar(sql: string): string | null {
  const rows = queryDb(sql);
  return rows.length ? rows[0][0] : null;
}

// ── FileSink inbox ─────────────────────────────────────────────────────────────────────────────

const SINK = process.env.PEMS_E2E_TEST_SINK_PATH;

export interface SinkRecord {
  to: Array<{ email: string; displayName?: string }>;
  cc: Array<{ email: string }>;
  bcc: Array<{ email: string }>;
  templateCode: string | null;
  subject: string;
  body: string;
  kind: string;
  code: string | null;
  link: string | null;
  at: string;
  status: string;
}

function readSink(): SinkRecord[] {
  if (!SINK) throw new Error('PEMS_E2E_TEST_SINK_PATH is not set — the real-stack harness must provide it.');
  let lines: string[] = [];
  try { lines = readFileSync(SINK, 'utf8').split('\n').filter(Boolean); } catch { /* not written yet */ }
  const out: SinkRecord[] = [];
  for (const line of lines) {
    try { out.push(JSON.parse(line) as SinkRecord); } catch { /* skip malformed */ }
  }
  return out;
}

/** How many messages the inbox holds right now — call before an action to scope the "after" search. */
export const sinkSize = () => readSink().length;

/**
 * Waits for a message matching `templateCode` addressed to `email`, considering ONLY records appended
 * after `since`. Scoping by position matters: these journeys re-use the same seeded accounts, so a
 * plain "find the newest DEPT_LEADERSHIP_GRANTED" would happily match a message an earlier test sent.
 */
export async function waitForEmail(
  templateCode: string, email: string, since = 0, timeoutMs = 15_000,
): Promise<SinkRecord> {
  const target = email.trim().toLowerCase();
  const deadline = Date.now() + timeoutMs;
  let seen: string[] = [];
  while (Date.now() < deadline) {
    const records = readSink().slice(since);
    const hit = records.find(
      r => r.templateCode === templateCode && r.to.some(t => t.email.toLowerCase() === target));
    if (hit) return hit;
    seen = records.map(r => `${r.templateCode}→${r.to.map(t => t.email).join(',')}`);
    await new Promise(r => setTimeout(r, 250));
  }
  throw new Error(
    `No ${templateCode} for ${email} within ${timeoutMs}ms. Seen since #${since}: ${seen.join(' | ') || '(nothing)'}`);
}

/** Asserts NO message of this template reached this address after `since`. */
export function expectNoEmail(templateCode: string, email: string, since = 0) {
  const target = email.trim().toLowerCase();
  const hit = readSink().slice(since).find(
    r => r.templateCode === templateCode && r.to.some(t => t.email.toLowerCase() === target));
  expect(hit, `Unexpected ${templateCode} was sent to ${email}`).toBeUndefined();
}

// ── Unique test data ───────────────────────────────────────────────────────────────────────────

/** A collision-proof suffix; the personnel table has a unique index on email. */
export const uniq = () => `${Date.now().toString(36)}${Math.floor(Math.random() * 1e4)}`;

/**
 * A unique suffix made only of LETTERS.
 *
 * Personal names are validated with AccountIdentityRules.IsValidFullName, which accepts letters and
 * the punctuation that occurs in real names (-'.) and nothing else. A digit-bearing suffix makes a
 * perfectly unique name that the API correctly rejects, which reads in the browser as "the dialog
 * never closed" rather than as "that name is invalid".
 */
export const uniqLetters = () =>
  Array.from(uniq(), c => (c >= "0" && c <= "9" ? String.fromCharCode(97 + Number(c)) : c)).join("");

// ── Stored-user shape ──────────────────────────────────────────────────────────────────────────

/**
 * The identity the app stores in localStorage, from the one `/auth/me` returned.
 *
 * `/auth/me` answers with `roleCode`, but the route guards in `App.tsx` read `user.role` — so a user
 * object copied straight from the API authenticates fine and then fails every role check, silently
 * redirecting to /dashboard. Setting both keeps the guard and the API agreeing about who this is.
 */
export const asStoredUser = (me: Record<string, unknown>) => ({
  ...me,
  role: me.role ?? me.roleCode,
  roleCode: me.roleCode ?? me.role,
});

// ── Authenticated API preconditions ────────────────────────────────────────────────────────────

export async function apiGet(request: APIRequestContext, path: string, profileKey: string) {
  const res = await request.get(`${API_BASE}${path}`, { headers: hdr(profileKey) });
  expect(res.ok(), `GET ${path} as ${profileKey} failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  return res.json();
}

export async function apiPost(request: APIRequestContext, path: string, profileKey: string, data: unknown) {
  const res = await request.post(`${API_BASE}${path}`, { headers: hdr(profileKey), data });
  expect(res.ok(), `POST ${path} as ${profileKey} failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  return res.json();
}

/** Performs a request expected to be REFUSED and returns the status, for the scope-denial journeys. */
export async function apiStatus(
  request: APIRequestContext, method: 'get' | 'post', path: string, profileKey: string, data?: unknown,
): Promise<number> {
  const res = method === 'get'
    ? await request.get(`${API_BASE}${path}`, { headers: hdr(profileKey) })
    : await request.post(`${API_BASE}${path}`, { headers: hdr(profileKey), data });
  return res.status();
}

/**
 * Asserts a request is refused with an EXACT status and, optionally, a stable error code.
 *
 * `status >= 400` is not good enough here, and settling for it is exactly how the defect this suite
 * uncovered stayed hidden: every business refusal in the reception-task handlers used to surface as
 * 500 INTERNAL_SERVER_ERROR, which satisfies ">= 400" perfectly well. Naming the status and the code
 * is what separates "the server correctly refused" from "the server fell over".
 */
export async function expectRefusal(
  request: APIRequestContext,
  method: 'get' | 'post',
  path: string,
  profileKey: string,
  expected: { status: number; errorCode?: string },
  data?: unknown,
): Promise<void> {
  const res = method === 'get'
    ? await request.get(`${API_BASE}${path}`, { headers: hdr(profileKey) })
    : await request.post(`${API_BASE}${path}`, { headers: hdr(profileKey), data });

  const body = await res.text();
  expect(res.status(), `${method.toUpperCase()} ${path} — expected ${expected.status}; body: ${body}`)
    .toBe(expected.status);

  if (expected.errorCode) {
    let parsed: { errorCode?: string } = {};
    try { parsed = JSON.parse(body) as { errorCode?: string }; } catch { /* fails the assertion below */ }
    expect(parsed.errorCode, `${method.toUpperCase()} ${path} — error code; body: ${body}`)
      .toBe(expected.errorCode);
  }
}
