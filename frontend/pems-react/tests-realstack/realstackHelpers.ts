/**
 * Shared REAL-STACK helpers for the authenticated DOM/UI journeys.
 *
 * real Chromium → real React (Vite) → real .NET API (Testing, flags ON, fail-closed E2E auth) → disposable
 * MySQL. NO network mocking. The browser/API request only ever carries an opaque profile KEY + the run
 * secret; identity is resolved SERVER-SIDE from the seeded profile file. Preconditions and post-action state
 * checks may use the authenticated API (allowed by §4/§5); the action UNDER TEST is always driven via the DOM.
 */
import { expect, type APIRequestContext, type Browser } from '@playwright/test';

export const API_BASE = process.env.PEMS_E2E_API_BASE ?? 'http://localhost:5299/api';
export const SECRET = process.env.PEMS_E2E_AUTH_SECRET ?? '';
export const API_PORT = new URL(API_BASE).port || '5299';
export const CAMPUS_HN = 1;
export const CAMPUS_HCM = 2;
/** A valid HN host candidate (proven by the API-level Journey F campus approve). */
export const HN_HOST_USER_ID = 3;

export const OWNER_USER = {
  userId: '8', fullName: 'External Visitor Main', email: 'visitor@example.com',
  roleCode: 'VISITOR', mustChangePassword: false, mustSetPassword: false,
};

/** E2E auth headers for a server-side profile key (identity resolved server-side, never from these headers). */
export const hdr = (profileKey: string) => ({ 'X-E2E-Profile': profileKey, 'X-E2E-Secret': SECRET });

/**
 * A browser page authenticated as a profile: inject the E2E headers on backend-origin API calls ONLY (never
 * on the Vite/static origin), and seed a logged-in session in localStorage. The stored user hydrates the UI;
 * the real E2E-authenticated `/auth/me` re-confirms the identity server-side.
 */
export async function authedPage(browser: Browser, profileKey: string, user: Record<string, unknown>) {
  const context = await browser.newContext();
  await context.route(new RegExp(`:${API_PORT}/`), async route => {
    await route.continue({ headers: { ...route.request().headers(), 'X-E2E-Profile': profileKey, 'X-E2E-Secret': SECRET } });
  });
  const page = await context.newPage();
  await page.addInitScript(u => {
    localStorage.setItem('token', 'e2e-session');
    localStorage.setItem('pems_user', JSON.stringify(u));
    localStorage.setItem('currentUser', JSON.stringify(u));
    localStorage.setItem('pems.language', 'vi');
  }, user);
  return { context, page };
}

/** Resolves the REAL identity of a profile via the E2E-authenticated `/auth/me` (so localStorage matches the
 * server-side actor exactly — roleCode/subRole/primaryCampusId included). */
export async function meUser(request: APIRequestContext, profileKey: string): Promise<Record<string, unknown>> {
  const res = await request.get(`${API_BASE}/auth/me`, { headers: hdr(profileKey) });
  expect(res.ok(), `auth/me failed for ${profileKey}: ${res.status()} ${await res.text()}`).toBeTruthy();
  const body = await res.json();
  return { ...body.user, mustChangePassword: false, mustSetPassword: false };
}

const pad = (n: number) => String(n).padStart(2, '0');
export const wallClock = (d: Date) =>
  `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;

/** A campus visit block for the v2 create payload (schedule well past the 24h/30-min rules). */
export function campusBlock(code: string, dayOffset: number, delegation: string, tag: string) {
  const start = new Date();
  start.setDate(start.getDate() + 20 + dayOffset);
  start.setHours(9, 0, 0, 0);
  const end = new Date(start.getTime() + 60 * 60 * 1000);
  return {
    campusId: code,
    plannedStartAt: wallClock(start),
    plannedEndAt: wallClock(end),
    delegationName: delegation,
    visitType: 'MEETING',
    visitTypeOther: null,
    purpose: `Muc dich ${tag}`,
    // Backend requires a non-empty working content per campus (Form.CampusVisits[].WorkingContent).
    workingContent: `Noi dung lam viec ${tag}`,
    visitors: [{ fullName: `Guest ${tag}`, nationality: 'VN', jobTitle: 'GV', organization: 'Org' }],
    externalSupportMembers: [],
    operationalContact: { fullName: 'Op Contact', organization: 'Org', phone: '+84900000001', email: 'op@example.com' },
    workingLanguage: 'EN',
    transportationNote: null,
    mediaConsentStatus: 'DECLINED',
    mediaConsentNote: null,
    notes: null,
    processing: null,
  };
}

export interface CreatedRequest {
  requestId: number;
  requestCode: string;
  instances: Array<{ visitInstanceId: number; campusId: number; status: string }>;
}

/** Creates a mixed HN+HCM v2 request as the owner through the REAL authenticated API. */
export async function createMixedRequest(
  request: APIRequestContext, tag: string, hnName: string, hcmName: string,
): Promise<CreatedRequest> {
  const res = await request.post(`${API_BASE}/v2/visit-requests`, {
    headers: hdr('visitor_owner'),
    data: {
      submissionId: `WF${tag}`,
      registrant: { fullName: 'Owner E2E', nationality: 'VN', organization: 'Org', jobTitle: 'Mgr', phone: '+84900000000', email: 'visitor@example.com' },
      primaryContact: { fullName: 'Owner E2E', organization: 'Org', phone: '+84900000000', email: 'visitor@example.com' },
      partnerId: null,
      campusVisits: [campusBlock('HN', 0, hnName, tag), campusBlock('HCM', 1, hcmName, tag)],
    },
  });
  expect(res.ok(), `create failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  const body = await res.json();
  expect(body.hasMixedCampusDetails).toBe(true);
  return { requestId: body.visitRequestId as number, requestCode: body.requestCode as string, instances: body.instances };
}

/** Reads the scoped v2 detail read-model for a viewer (default: the owner sees every campus). */
export async function readDetail(request: APIRequestContext, requestId: number, profileKey = 'visitor_owner') {
  const res = await request.get(`${API_BASE}/v2/visit-requests/${requestId}`, { headers: hdr(profileKey) });
  expect(res.ok(), `read detail failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  return res.json();
}

export const campusOf = (detail: any, campusId: number) =>
  detail.campusVisits.find((c: any) => c.campusId === campusId);

/** Precondition: a campus leader approves their campus (→ ASSIGNED). */
export async function approveCampus(
  request: APIRequestContext, requestId: number, instanceId: number, leaderKey: string, hostUserId = HN_HOST_USER_ID,
) {
  const res = await request.post(`${API_BASE}/delegations/${requestId}/campuses/${instanceId}/approve`, {
    headers: hdr(leaderKey), data: { hostUserId, decisionNote: 'assign' },
  });
  expect(res.ok(), `campus approve failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  return res.json();
}

/** Precondition: a campus leader rejects their campus (→ REJECTED). */
export async function rejectCampus(
  request: APIRequestContext, requestId: number, instanceId: number, leaderKey: string,
) {
  const res = await request.post(`${API_BASE}/delegations/${requestId}/campuses/${instanceId}/reject`, {
    headers: hdr(leaderKey), data: { reason: 'Chua dat yeu cau' },
  });
  expect(res.ok(), `campus reject failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  return res.json();
}

/** Precondition: the owner submits a member amendment for a campus via the API (used when the SUBMIT is NOT
 * the action under test — e.g. the leader-approve / reject / withdraw journeys). Adds one guest, keeps the rest. */
export async function submitAmendmentApi(
  request: APIRequestContext, requestId: number, instanceId: number, hn: any, extraGuestName: string,
): Promise<number> {
  const proposal = {
    expectedInstanceRowVersion: hn.rowVersion,
    baseFormRevision: hn.formRevision,
    baseApprovalRevision: hn.approvalRevision,
    reason: 'Them khach (precondition)',
    delegationName: hn.delegationName,
    visitType: hn.visitType,
    visitTypeOther: hn.visitTypeOther ?? null,
    purpose: hn.purpose,
    // Backend requires non-empty working content; never fall back to null on read-back.
    workingContent: hn.workingContent ?? 'Noi dung lam viec (amendment)',
    workingLanguage: hn.workingLanguage,
    operationalContact: hn.operationalContact,
    visitors: [
      ...hn.visitors.map((v: any) => ({ fullName: v.fullName, nationality: v.nationality, jobTitle: v.jobTitle, organization: v.organization })),
      { fullName: extraGuestName, nationality: 'VN', jobTitle: 'GV', organization: 'Org' },
    ],
    externalSupportMembers: (hn.supportMembers ?? []).map((v: any) => ({ fullName: v.fullName, jobTitle: v.jobTitle, organization: v.organization, nationality: v.nationality })),
    plannedStartAt: hn.plannedStartAt,
    plannedEndAt: hn.plannedEndAt,
  };
  const res = await request.post(`${API_BASE}/v2/visit-requests/${requestId}/instances/${instanceId}/amendments`, { headers: hdr('visitor_owner'), data: proposal });
  expect(res.ok(), `amendment submit (precondition) failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  return (await res.json()).amendmentId as number;
}
