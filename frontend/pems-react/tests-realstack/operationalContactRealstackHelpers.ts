/**
 * Shared REAL-STACK helpers for the Operational Contact live-browser journeys (FLOW 01-08 + the
 * Registrant→Visitor→MEMBER smoke).
 *
 * real Chromium → real React (Vite) → real .NET API (Testing, flags ON, fail-closed E2E auth) →
 * disposable MySQL. NO network mocking. Preconditions are created through the REAL authenticated API
 * (same convention as `realstackHelpers.ts`); the action under test is always driven through the DOM.
 */
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { expect, type APIRequestContext } from '@playwright/test';
import { API_BASE, hdr, FIXTURE_REGISTRANT_EMAIL, wallClock } from './realstackHelpers';
import type { SinkRecord } from './sinkRecord';

export { API_BASE, hdr, authedPage, meUser, OWNER_USER, CAMPUS_HN, CAMPUS_HCM } from './realstackHelpers';

/** Kim IS the registrant/owner (`kim.minjae@seoultech.example`) — every fixture below uses her as both,
 * so a campus whose contact snapshot also carries her address self-confirms at create
 * (`VisitRequestV2CreateService`: REGISTRANT_SELF_MATCH) without a separate confirmation step. */
export const KIM = {
  fullName: 'Kim Min Jae', organization: 'SeoulTech', jobTitle: 'Director',
  phone: '+84900000001', email: FIXTURE_REGISTRANT_EMAIL, nationality: 'KR',
};

/** A second, unrelated member — used wherever a flow needs somebody who is NOT the linked contact. */
export const MOON = {
  fullName: 'Moon Ji Woo', organization: 'SeoulTech', jobTitle: 'Deputy Director', nationality: 'KR',
};

const pad = (n: number) => String(n).padStart(2, '0');
const dateKey = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

/**
 * ONE campus block for the v2 create payload, with Kim as BOTH the sole visitor AND the operational
 * contact — linked via `operationalContactClientMemberKey` (NP-03) so the campus starts life with a
 * real MEMBER-sourced relation, not just a matching snapshot. `dayOffset` keeps sibling campuses on a
 * mixed request from colliding on the same slot.
 */
export function campusBlockKimLinked(campusId: string, dayOffset: number, tag: string, delegationName: string) {
  const start = new Date();
  start.setDate(start.getDate() + 20 + dayOffset);
  start.setHours(9, 0, 0, 0);
  const end = new Date(start.getTime() + 60 * 60 * 1000);
  const kimKey = `kim-${tag}-${campusId}`;
  return {
    campusId,
    plannedStartAt: wallClock(start),
    plannedEndAt: wallClock(end),
    delegationName,
    visitType: 'MEETING',
    visitTypeOther: null,
    purpose: `Muc dich ${tag}`,
    workingContent: `Noi dung lam viec ${tag}`,
    visitors: [{
      fullName: KIM.fullName, nationality: KIM.nationality, jobTitle: KIM.jobTitle, organization: KIM.organization,
      organizationPartnerId: null, clientMemberKey: kimKey, guestMemberId: null,
    }],
    externalSupportMembers: [],
    operationalContact: {
      fullName: KIM.fullName, organization: KIM.organization, jobTitle: KIM.jobTitle,
      phone: KIM.phone, email: KIM.email,
    },
    workingLanguage: 'EN',
    transportationNote: null,
    mediaConsentStatus: 'DECLINED',
    notes: null,
    hostSelection: null,
    operationalContactClientMemberKey: kimKey,
  };
}

/**
 * Generalized version of {@link campusBlockKimLinked}: a campus whose SOLE visitor is `person`, linked
 * as the operational contact via `operationalContactClientMemberKey`. Used where a flow needs a target
 * campus with its OWN linked member (not Kim) — e.g. Apply-To-All's protected-member-orphan case.
 */
export function campusBlockLinkedPerson(
  campusId: string, dayOffset: number, tag: string, delegationName: string,
  person: { fullName: string; organization: string; jobTitle: string; nationality: string },
  contactEmail: string,
) {
  const start = new Date();
  start.setDate(start.getDate() + 20 + dayOffset);
  start.setHours(9, 0, 0, 0);
  const end = new Date(start.getTime() + 60 * 60 * 1000);
  const key = `${person.fullName.replace(/\s+/g, '')}-${tag}-${campusId}`;
  return {
    campusId,
    plannedStartAt: wallClock(start),
    plannedEndAt: wallClock(end),
    delegationName,
    visitType: 'MEETING',
    visitTypeOther: null,
    purpose: `Muc dich ${tag}`,
    workingContent: `Noi dung lam viec ${tag}`,
    visitors: [{
      fullName: person.fullName, nationality: person.nationality, jobTitle: person.jobTitle,
      organization: person.organization, organizationPartnerId: null, clientMemberKey: key, guestMemberId: null,
    }],
    externalSupportMembers: [],
    operationalContact: {
      fullName: person.fullName, organization: person.organization, jobTitle: person.jobTitle,
      phone: '+84900000002', email: contactEmail,
    },
    workingLanguage: 'EN',
    transportationNote: null,
    mediaConsentStatus: 'DECLINED',
    notes: null,
    hostSelection: null,
    operationalContactClientMemberKey: key,
  };
}

/** Same shape, but the contact is a plain EXTERNAL snapshot (Lee) — nobody in `visitors` matches it. */
export function campusBlockExternalLee(campusId: string, dayOffset: number, tag: string, delegationName: string) {
  const start = new Date();
  start.setDate(start.getDate() + 20 + dayOffset);
  start.setHours(9, 0, 0, 0);
  const end = new Date(start.getTime() + 60 * 60 * 1000);
  return {
    campusId,
    plannedStartAt: wallClock(start),
    plannedEndAt: wallClock(end),
    delegationName,
    visitType: 'MEETING',
    visitTypeOther: null,
    purpose: `Muc dich ${tag}`,
    workingContent: `Noi dung lam viec ${tag}`,
    visitors: [{
      fullName: `Guest ${tag}`, nationality: 'VN', jobTitle: 'GV', organization: 'Org',
      organizationPartnerId: null, clientMemberKey: `guest-${tag}-${campusId}`, guestMemberId: null,
    }],
    externalSupportMembers: [],
    operationalContact: {
      fullName: 'Lee Sang Hoon', organization: 'Lee Org', jobTitle: 'Coordinator',
      phone: '+84900000099', email: `lee.${tag}@example.com`,
    },
    workingLanguage: 'EN',
    transportationNote: null,
    mediaConsentStatus: 'DECLINED',
    notes: null,
    hostSelection: null,
    operationalContactClientMemberKey: null,
  };
}

export interface CreatedRequest {
  requestId: number;
  requestCode: string;
  instances: Array<{ visitInstanceId: number; campusId: string; status: string }>;
}

/** Creates a v2 request as Kim (owner) through the REAL authenticated API — the shared setup step every
 * flow below starts from, per plan §B ("preconditions … through the REAL authenticated API"). */
export async function createKimRequest(
  request: APIRequestContext, tag: string, campusVisits: unknown[],
): Promise<CreatedRequest> {
  const res = await request.post(`${API_BASE}/v2/visit-requests`, {
    headers: hdrLocal('visitor_owner'),
    data: {
      submissionId: `OC${tag}`,
      registrant: {
        fullName: KIM.fullName, nationality: KIM.nationality, organization: KIM.organization,
        jobTitle: KIM.jobTitle, phone: KIM.phone, email: KIM.email,
      },
      partnerId: null,
      campusVisits,
    },
  });
  expect(res.ok(), `create failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  const body = await res.json();
  return { requestId: body.visitRequestId as number, requestCode: body.requestCode as string, instances: body.instances };
}

// Re-declared locally (not re-exported above with a different name) purely so this file's own API calls
// don't depend on import ordering quirks with the re-export line.
function hdrLocal(profileKey: string) {
  const SECRET = process.env.PEMS_E2E_AUTH_SECRET ?? '';
  return { 'X-E2E-Profile': profileKey, 'X-E2E-Secret': SECRET };
}

/** Reads the scoped v2 detail read-model. */
export async function readDetail(request: APIRequestContext, requestId: number, profileKey = 'visitor_owner') {
  const res = await request.get(`${API_BASE}/v2/visit-requests/${requestId}`, { headers: hdrLocal(profileKey) });
  expect(res.ok(), `read detail failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  return res.json();
}

export const campusOf = (detail: any, campusId: string) =>
  detail.campusVisits.find((c: any) => c.campusId === campusId || c.campusCode === campusId);

// ── Read-only SQL against the disposable database (same convention as emailRealstackHelpers.ts) ──────

const PROTECTED_DATABASES = ['pems_db', 'pems_test', 'pems_pr3_test'];
const DB = process.env.PEMS_E2E_DB ?? 'pems_e2e_realstack';

export function queryDb(sql: string): string[][] {
  if (PROTECTED_DATABASES.includes(DB)) throw new Error(`Refusing to query a protected database: ${DB}`);
  if (!/^\s*select\b/i.test(sql)) throw new Error(`queryDb is read-only; refusing: ${sql.slice(0, 60)}`);
  const r = spawnSync(
    process.env.MYSQL_BIN ?? 'mysql',
    [`-u${process.env.MYSQL_USER ?? 'root'}`, `-p${process.env.MYSQL_PASSWORD ?? '123456'}`,
      `-h${process.env.MYSQL_HOST ?? 'localhost'}`, `-P${process.env.MYSQL_PORT ?? '3306'}`,
      '-N', '-B', '--default-character-set=utf8mb4', DB, '-e', sql],
    { encoding: 'utf8' },
  );
  if (r.status !== 0) throw new Error(`queryDb failed: ${r.stderr}`);
  return (r.stdout ?? '').trim().split(/\r?\n/).filter(Boolean).map(line => line.split('\t'));
}

export function scalar(sql: string): string | null {
  const rows = queryDb(sql);
  return rows.length ? rows[0][0] : null;
}

// ── FileSink email reading (same convention as departmentRealstackHelpers.ts) ─────────────────────────

const SINK = process.env.PEMS_E2E_TEST_SINK_PATH;

function readSink(): SinkRecord[] {
  if (!SINK) throw new Error('PEMS_E2E_TEST_SINK_PATH is not set — the real-stack harness must provide it.');
  let lines: string[] = [];
  try { lines = readFileSync(SINK, 'utf8').split('\n').filter(Boolean); } catch { /* not written yet */ }
  const out: SinkRecord[] = [];
  for (const line of lines) {
    try { out.push(JSON.parse(line) as SinkRecord); } catch { /* partial write */ }
  }
  return out;
}

export const sinkSize = () => readSink().length;

/** Waits for a message matching `templateCode` addressed to `email`, considering only records appended
 * after `since` — scoping by position so a re-used seeded/typed address never matches an earlier test's mail. */
export async function waitForContactEmail(
  templateCode: string, email: string, since = 0, timeoutMs = 15_000,
): Promise<SinkRecord> {
  const target = email.trim().toLowerCase();
  const deadline = Date.now() + timeoutMs;
  let seen: string[] = [];
  while (Date.now() < deadline) {
    const records = readSink().slice(since);
    const hit = records.find(r =>
      r.templateCode === templateCode && (r.to ?? []).some(t => (t.email ?? '').toLowerCase() === target));
    if (hit) return hit;
    seen = records.map(r => `${r.templateCode}→${(r.to ?? []).map(t => t.email).join(',')}`);
    await new Promise(res => setTimeout(res, 250));
  }
  throw new Error(
    `No ${templateCode} for ${email} within ${timeoutMs}ms. Seen since #${since}: ${seen.join(' | ') || '(nothing)'}`);
}

export const dateKeyOf = dateKey;
