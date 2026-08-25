/**
 * REAL-STACK E2E — authenticated v2 workflow journeys (D/G/H) on the fail-closed E2E auth scheme.
 *
 * real Chromium → real React (Vite) → real .NET API (Testing, flags ON, fail-closed E2E auth) → disposable
 * MySQL. NO network mocking. Preconditions are created through the REAL authenticated API (allowed by §4);
 * the action under test is driven through the real UI (Journey D) or asserted at the real host (H scope, G
 * denial). The browser/API request only ever carries an opaque profile KEY + the run secret.
 *
 * Journey H — scope-safe search: a keyword that exists only on a HIDDEN sibling campus never surfaces the
 *             request for a campus-scoped actor, and match contexts are scoped to authorized campuses.
 * Journey G — wrong-campus denial: a campus leader cannot decide another campus's amendment (403 at the host).
 * Journey D — authenticated create through the real UI + the v2 detail read.
 */
import { test, expect, type APIRequestContext, type Browser } from '@playwright/test';

const API_BASE = process.env.PEMS_E2E_API_BASE ?? 'http://localhost:5299/api';
const SECRET = process.env.PEMS_E2E_AUTH_SECRET ?? '';
const API_PORT = new URL(API_BASE).port || '5299';
const CAMPUS_HN = 1;
const CAMPUS_HCM = 2;

const OWNER_USER = {
  userId: '8', fullName: 'Kim Min Jae', email: 'kim.minjae@seoultech.example',
  roleCode: 'VISITOR', mustChangePassword: false, mustSetPassword: false,
};

/** E2E auth headers for a server-side profile key (identity resolved server-side, never from these headers). */
const hdr = (profileKey: string) => ({ 'X-E2E-Profile': profileKey, 'X-E2E-Secret': SECRET });

/** A browser page authenticated as a profile: seed a logged-in session + inject the E2E headers on API calls. */
async function authedPage(browser: Browser, profileKey: string, user: Record<string, unknown>) {
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

const pad = (n: number) => String(n).padStart(2, '0');
const wallClock = (d: Date) =>
  `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;

/** A campus visit block for the v2 create payload (schedule well past the 24h/30-min rules). */
function campusBlock(code: string, dayOffset: number, delegation: string, tag: string) {
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
    // Matches the registrant below, so the campus self-confirms at create and the confirmation gate
    // is already open when these journeys approve it — see FIXTURE_REGISTRANT_EMAIL in realstackHelpers.
    operationalContact: { fullName: 'Op Contact', organization: 'Org', jobTitle: 'Trưởng phòng Hợp tác', phone: '+84900000001', email: 'kim.minjae@seoultech.example' },
    workingLanguage: 'EN',
    transportationNote: null,
    mediaConsentStatus: 'DECLINED',
    notes: null,
    processing: null,
  };
}

interface CreatedRequest {
  requestId: number;
  instances: Array<{ visitInstanceId: number; campusId: number; status: string }>;
}

/** Creates a mixed HN+HCM v2 request as the owner through the REAL authenticated API. */
async function createMixedRequest(request: APIRequestContext, tag: string, hnName: string, hcmName: string): Promise<CreatedRequest> {
  const res = await request.post(`${API_BASE}/v2/visit-requests`, {
    headers: hdr('visitor_owner'),
    data: {
      submissionId: `WF${tag}`,
      registrant: { fullName: 'Owner E2E', nationality: 'VN', organization: 'Org', jobTitle: 'Mgr', phone: '+84900000000', email: 'kim.minjae@seoultech.example' },
      partnerId: null,
      campusVisits: [campusBlock('HN', 0, hnName, tag), campusBlock('HCM', 1, hcmName, tag)],
    },
  });
  expect(res.ok(), `create failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  const body = await res.json();
  expect(body.hasMixedCampusDetails).toBe(true);
  return { requestId: body.visitRequestId as number, instances: body.instances };
}

const listUrl = (keyword: string) =>
  `${API_BASE}/delegations/viewguestdelegationlist?tab=responsible&page=1&pageSize=100&keyword=${encodeURIComponent(keyword)}`;

test.describe('Real-stack: authenticated v2 workflow journeys', () => {
  test('Journey H — search is scope-safe: a hidden-campus keyword never leaks and contexts stay authorized', async ({ request }) => {
    expect(SECRET).not.toBe('');
    const tag = `H${Date.now().toString(36)}`;
    const alpha = `AlphaKW${tag}`; // HN delegation
    const beta = `BetaKW${tag}`;   // HCM delegation (the hidden sibling for the HN leader)
    const { requestId } = await createMixedRequest(request, tag, `Doan ${alpha}`, `Doan ${beta}`);

    // The HN Staff Leader searching HCM's keyword must NOT surface the request (hidden-campus no leak).
    const hiddenRes = await request.get(listUrl(beta), { headers: hdr('campus_leader_hn') });
    expect(hiddenRes.ok()).toBeTruthy();
    const hiddenItems = (await hiddenRes.json()).items as Array<{ visitRequestId: number }>;
    expect(hiddenItems.some(i => i.visitRequestId === requestId)).toBe(false);

    // Searching HN's own keyword surfaces the request with a context scoped to HN only (never HCM).
    const ownRes = await request.get(listUrl(alpha), { headers: hdr('campus_leader_hn') });
    const ownRow = ((await ownRes.json()).items as any[]).find(i => i.visitRequestId === requestId);
    expect(ownRow, 'HN leader should see the request under its own keyword').toBeTruthy();
    const campusCtxs = (ownRow.matchedContexts ?? []).filter((c: any) => c.scope === 'CAMPUS');
    expect(campusCtxs.length).toBeGreaterThanOrEqual(1);
    expect(campusCtxs.every((c: any) => c.campusId === CAMPUS_HN)).toBe(true); // never the hidden HCM campus
    expect(campusCtxs.some((c: any) => c.matchedFields.includes('DELEGATION_NAME'))).toBe(true);

    // The owner sees ALL their campuses → HCM's keyword surfaces the request with an HCM context.
    const ownerRes = await request.get(listUrl(beta), { headers: hdr('visitor_owner') });
    const ownerRow = ((await ownerRes.json()).items as any[]).find(i => i.visitRequestId === requestId);
    expect(ownerRow, 'owner should see the request under any of their campus keywords').toBeTruthy();
    const ownerCampusCtxs = (ownerRow.matchedContexts ?? []).filter((c: any) => c.scope === 'CAMPUS');
    expect(ownerCampusCtxs.some((c: any) => c.campusId === CAMPUS_HCM)).toBe(true);
  });

  test('Journey D — an authenticated owner opens the per-campus v2 detail through the real UI', async ({ browser, request }) => {
    const tag = `D${Date.now().toString(36)}`;
    const { requestId, instances } = await createMixedRequest(request, tag, `Doan HN ${tag}`, `Doan HCM ${tag}`);
    const hcmInstance = instances.find(i => i.campusId === CAMPUS_HCM)!.visitInstanceId;

    const { context, page } = await authedPage(browser, 'visitor_owner', OWNER_USER);
    try {
      await page.goto(`/dashboard/visit/v2/${requestId}`);
      // The v2 detail renders BOTH authorized campus cards, but on a multi-campus request only the
      // FIRST is expanded by default (VisitRequestV2DetailView's openCampusIds) — the rest start
      // collapsed, showing only their header (campus/status), never the delegation name. The owner
      // still sees every campus of their own request; the ones after the first just need a click.
      await expect(page.getByText(`Doan HN ${tag}`)).toBeVisible({ timeout: 25_000 });
      await page.getByTestId(`campus-detail-toggle-${hcmInstance}`).click();
      await expect(page.getByText(`Doan HCM ${tag}`)).toBeVisible({ timeout: 15_000 });
      await expect(page).toHaveURL(new RegExp(`/dashboard/visit/v2/${requestId}`));
    } finally {
      await context.close();
    }
  });

  test('Journey G — a wrong-campus leader is denied deciding another campus at the real host', async ({ request }) => {
    const tag = `G${Date.now().toString(36)}`;
    const { requestId, instances } = await createMixedRequest(request, tag, `GHN ${tag}`, `GHCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;

    // Amendment decisions are gated on the CURRENT HOST of the instance (DecideVisitAmendmentCommandHandlers
    // -> AmendmentGuards.EnsureCurrentHost), not on Staff Leader scope alone -- nobody holds that authority
    // until a campus is approved with a named host, so the HN leader must actually self-host HN first, the
    // same optimistic-concurrency precondition every other approve call in this suite needs.
    const detail = await (await request.get(`${API_BASE}/v2/visit-requests/${requestId}`, { headers: hdr('visitor_owner') })).json();
    const hnRow = detail.campusVisits.find((c: any) => c.campusId === CAMPUS_HN);
    const approve = await request.post(`${API_BASE}/delegations/${requestId}/campuses/${hnInstance}/approve`, {
      headers: hdr('campus_leader_hn'),
      data: { hostUserId: 3, decisionNote: 'assign', expectedInstanceRowVersion: hnRow.rowVersion },
    });
    expect(approve.ok(), `campus approve failed: ${approve.status()} ${await approve.text()}`).toBeTruthy();

    // The HCM leader has no authority over the HN campus: the amendment-approve endpoint 403s on the host
    // gate, so a fabricated amendment id is enough to prove the scope check — the browser could never
    // surface this action, and the host refuses it directly.
    const wrong = await request.post(`${API_BASE}/v2/visit-instances/${hnInstance}/amendments/999999999/approve`, {
      headers: hdr('campus_leader_hcm'), data: { note: 'nope' },
    });
    expect(wrong.status()).toBe(403);

    // The HN leader — now HN's current Host — passes the host gate (so it is NOT a blanket 403); the
    // fabricated amendment then 404s.
    const right = await request.post(`${API_BASE}/v2/visit-instances/${hnInstance}/amendments/999999999/approve`, {
      headers: hdr('campus_leader_hn'), data: { note: 'ok' },
    });
    expect(right.status(), `HN leader should pass the host gate, got ${right.status()}`).not.toBe(403);
    expect([400, 404, 409]).toContain(right.status());
  });

  test('Journey E — pending-edit changes only the target campus and leaves the sibling untouched', async ({ request }) => {
    const tag = `E${Date.now().toString(36)}`;
    const { requestId } = await createMixedRequest(request, tag, `EHN ${tag}`, `EHCM ${tag}`);

    const readDetail = async () =>
      (await request.get(`${API_BASE}/v2/visit-requests/${requestId}`, { headers: hdr('visitor_owner') })).json();
    const before = await readDetail();
    const hnBefore = before.campusVisits.find((c: any) => c.campusId === CAMPUS_HN);
    const hcmBefore = before.campusVisits.find((c: any) => c.campusId === CAMPUS_HCM);

    const newHn = `EHN-edited ${tag}`;
    const editBlock = (code: string, off: number, name: string, inst: number, rv: number) =>
      ({ ...campusBlock(code, off, name, tag), visitInstanceId: inst, expectedRowVersion: rv });

    // Edit HN's delegation; resend HCM UNCHANGED so the change-detector treats it as a true no-op.
    const edit = await request.put(`${API_BASE}/v2/visit-requests/${requestId}/pending-edit`, {
      headers: hdr('visitor_owner'),
      data: {
        expectedRequestRowVersion: before.rowVersion,
        registrant: { fullName: 'Owner E2E', nationality: 'VN', organization: 'Org', jobTitle: 'Mgr', phone: '+84900000000', email: 'kim.minjae@seoultech.example' },
        partnerId: null,
        campusVisits: [
          editBlock('HN', 0, newHn, hnBefore.visitInstanceId, hnBefore.rowVersion),
          editBlock('HCM', 1, `EHCM ${tag}`, hcmBefore.visitInstanceId, hcmBefore.rowVersion),
        ],
      },
    });
    expect(edit.ok(), `pending-edit failed: ${edit.status()} ${await edit.text()}`).toBeTruthy();

    const after = await readDetail();
    const hnAfter = after.campusVisits.find((c: any) => c.campusId === CAMPUS_HN);
    const hcmAfter = after.campusVisits.find((c: any) => c.campusId === CAMPUS_HCM);
    expect(hnAfter.delegationName).toBe(newHn);          // target campus changed
    expect(hcmAfter.delegationName).toBe(`EHCM ${tag}`); // sibling delegation untouched
    expect(hnAfter.rowVersion).toBeGreaterThan(hnBefore.rowVersion); // changed campus bumped
    expect(hcmAfter.rowVersion).toBe(hcmBefore.rowVersion);          // sibling is a true no-op
  });

  test('Journey F — member amendment: submit keeps the active snapshot, leader-approve applies target-only', async ({ request }) => {
    const tag = `F${Date.now().toString(36)}`;
    const { requestId, instances } = await createMixedRequest(request, tag, `FHN ${tag}`, `FHCM ${tag}`);
    const hnInstance = instances.find(i => i.campusId === CAMPUS_HN)!.visitInstanceId;

    const readHn = async () => {
      const d = await (await request.get(`${API_BASE}/v2/visit-requests/${requestId}`, { headers: hdr('visitor_owner') })).json();
      return { hn: d.campusVisits.find((c: any) => c.campusId === CAMPUS_HN), hcm: d.campusVisits.find((c: any) => c.campusId === CAMPUS_HCM) };
    };

    // Precondition: the HN leader approves the HN campus (self-host) → HN becomes ASSIGNED (amendable, >24h out).
    // The approve endpoint requires the campus's current rowVersion as an optimistic-concurrency token
    // (VISIT_INSTANCE_VERSION_REQUIRED otherwise) — read it fresh first, same as the shared `approveCampus`
    // helper in realstackHelpers.ts does.
    const s0 = await readHn();
    const approve = await request.post(`${API_BASE}/delegations/${requestId}/campuses/${hnInstance}/approve`, {
      headers: hdr('campus_leader_hn'),
      data: { hostUserId: 3, decisionNote: 'assign', expectedInstanceRowVersion: s0.hn.rowVersion },
    });
    expect(approve.ok(), `campus approve failed: ${approve.status()} ${await approve.text()}`).toBeTruthy();

    const s1 = await readHn();
    expect(s1.hn.instanceStatus).toBe('ASSIGNED');
    const originalCount = s1.hn.visitors.length;

    // The owner submits a member amendment (adds a guest, keeps everything else). Reason is required.
    const proposal = {
      expectedInstanceRowVersion: s1.hn.rowVersion,
      baseFormRevision: s1.hn.formRevision,
      baseApprovalRevision: s1.hn.approvalRevision,
      reason: 'Them khach',
      delegationName: s1.hn.delegationName,
      visitType: s1.hn.visitType,
      visitTypeOther: s1.hn.visitTypeOther ?? null,
      purpose: s1.hn.purpose,
      // Backend requires non-empty working content; never fall back to null on read-back.
      workingContent: s1.hn.workingContent ?? 'Noi dung lam viec (amendment)',
      workingLanguage: s1.hn.workingLanguage,
      operationalContact: s1.hn.operationalContact,
      visitors: [
        ...s1.hn.visitors.map((v: any) => ({ fullName: v.fullName, nationality: v.nationality, jobTitle: v.jobTitle, organization: v.organization })),
        { fullName: `Guest2 ${tag}`, nationality: 'VN', jobTitle: 'GV', organization: 'Org' },
      ],
      externalSupportMembers: (s1.hn.supportMembers ?? []).map((v: any) => ({ fullName: v.fullName, jobTitle: v.jobTitle, organization: v.organization, nationality: v.nationality })),
      plannedStartAt: s1.hn.plannedStartAt,
      plannedEndAt: s1.hn.plannedEndAt,
    };
    const submit = await request.post(`${API_BASE}/v2/visit-requests/${requestId}/instances/${hnInstance}/amendments`, { headers: hdr('visitor_owner'), data: proposal });
    expect(submit.ok(), `amendment submit failed: ${submit.status()} ${await submit.text()}`).toBeTruthy();
    const amendmentId = (await submit.json()).amendmentId;

    // BEFORE approval: the ACTIVE snapshot must be unchanged (still the original guests) and a pending amendment noted.
    const s2 = await readHn();
    expect(s2.hn.visitors.length).toBe(originalCount);
    expect(s2.hn.activeAmendment?.status).toBe('PENDING_APPROVAL');

    // The CURRENT campus leader approves → applied target-only; the sibling campus is untouched.
    const approveAmend = await request.post(`${API_BASE}/v2/visit-instances/${hnInstance}/amendments/${amendmentId}/approve`, { headers: hdr('campus_leader_hn'), data: { note: 'ok' } });
    expect(approveAmend.ok(), `amendment approve failed: ${approveAmend.status()} ${await approveAmend.text()}`).toBeTruthy();

    const s3 = await readHn();
    expect(s3.hn.visitors.length).toBe(originalCount + 1);
    expect(s3.hn.visitors.some((v: any) => v.fullName === `Guest2 ${tag}`)).toBe(true);
    expect(s3.hcm.delegationName).toBe(`FHCM ${tag}`); // sibling delegation untouched by the HN amendment
  });
});
