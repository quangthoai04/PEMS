/**
 * Helpers for the notification-routing REAL-STACK live-browser verification
 * (PEMS_NOTIFICATION_ROUTING_STABILIZATION_FULL_IMPLEMENTATION_PLAN.md, live-browser follow-up round).
 *
 * Same contract as the other real-stack helpers: real Chromium -> real React -> real .NET API
 * (Testing, fail-closed E2E auth) -> disposable MySQL. NO network mocking of DATA. A handful of
 * scenarios in the plan (legacy/pre-migration rows with no eventKey, an edge-case row missing an
 * instanceId) cannot be produced through any live business action -- no producer in the current
 * codebase writes a null-metadata row or omits an instanceId on a campus-specific event -- so those
 * are seeded directly into the disposable DB with EXACTLY the shape a real row of that kind would
 * have (same columns, same constants). This mirrors the plan's own instruction for the "Legacy
 * unknown" scenarios ("Tao fixture notification cu"). Every other scenario in the spec is driven
 * through a real backend command via the authenticated API or the real DOM.
 */
import { spawnSync } from 'node:child_process';
import { expect, type APIRequestContext } from '@playwright/test';
import { API_BASE, hdr } from './realstackHelpers';

const PROTECTED_DATABASES = ['pems_db', 'pems_test', 'pems_pr3_test'];
const DB = process.env.PEMS_E2E_DB ?? 'pems_e2e_realstack';

function mysqlArgs(extra: string[]): string[] {
  return [
    `-u${process.env.MYSQL_USER ?? 'root'}`,
    `-p${process.env.MYSQL_PASSWORD ?? '123456'}`,
    `-h${process.env.MYSQL_HOST ?? 'localhost'}`,
    `-P${process.env.MYSQL_PORT ?? '3306'}`,
    '--default-character-set=utf8mb4',
    ...extra,
  ];
}

/** Read-only query against the disposable database. */
export function queryDb(sql: string): string[][] {
  if (PROTECTED_DATABASES.includes(DB)) throw new Error(`Refusing to query a protected database: ${DB}`);
  if (!/^\s*select\b/i.test(sql)) throw new Error(`queryDb is read-only; refusing: ${sql.slice(0, 80)}`);
  const r = spawnSync(process.env.MYSQL_BIN ?? 'mysql', mysqlArgs(['-N', '-B', DB, '-e', sql]), { encoding: 'utf8' });
  if (r.status !== 0) throw new Error(`queryDb failed: ${r.stderr}`);
  return (r.stdout ?? '').trim().split(/\r?\n/).filter(Boolean).map(line => line.split('\t'));
}

export function scalar(sql: string): string | null {
  const rows = queryDb(sql);
  return rows.length ? rows[0][0] : null;
}

/**
 * Write against the disposable database ONLY -- refuses a protected name and refuses anything that
 * is not an INSERT/UPDATE, so a typo here can never touch a shared database or run an arbitrary
 * statement. Used exclusively to seed the handful of fixture rows no live producer can create today
 * (see file header).
 */
function execDb(sql: string): void {
  if (PROTECTED_DATABASES.includes(DB)) throw new Error(`Refusing to write to a protected database: ${DB}`);
  if (!/^\s*(insert|update)\b/i.test(sql)) throw new Error(`execDb refuses a non-insert/update statement: ${sql.slice(0, 80)}`);
  const r = spawnSync(process.env.MYSQL_BIN ?? 'mysql', mysqlArgs([DB, '-e', sql]), { encoding: 'utf8' });
  if (r.status !== 0) throw new Error(`execDb failed: ${r.stderr}\nSQL: ${sql}`);
}

const esc = (s: string) => s.replace(/\\/g, '\\\\').replace(/'/g, "\\'");

export function userIdOf(email: string): number {
  const id = scalar(`SELECT user_id FROM users WHERE email = '${esc(email)}'`);
  if (!id) throw new Error(`No seeded user for email ${email}`);
  return Number(id);
}

/**
 * Seeds a notification row with NO metadataJson (the pre-eventKey-migration shape) and the generic
 * legacy actionType every such historical row carries. Plan section 15 ("LEGACY UNKNOWN") asks for
 * exactly this fixture: no live producer writes a row like this today, so it cannot be produced any
 * other way, and the plan explicitly sanctions seeding it directly for this one scenario.
 */
export function insertLegacyNotification(opts: {
  recipientEmail: string; visitRequestId: number; visitInstanceId?: number | null; title: string;
}): number {
  const recipientId = userIdOf(opts.recipientEmail);
  const url = opts.visitInstanceId
    ? `/dashboard/visit?visitRequestId=${opts.visitRequestId}&visitInstanceId=${opts.visitInstanceId}`
    : `/dashboard/visit?visitRequestId=${opts.visitRequestId}`;
  execDb(`INSERT INTO notifications
    (recipient_user_id, title, message, notification_type, category, priority, is_action_required,
     related_type, related_id, visit_request_id, visit_instance_id, campus_id,
     action_type, action_url, metadata_json, is_read, created_at)
    VALUES (${recipientId}, '${esc(opts.title)}', 'Legacy fixture row (E2E) -- no metadataJson.',
     'LEGACY_TEST', 'VISIT', 'NORMAL', FALSE,
     'VisitRequest', ${opts.visitRequestId}, ${opts.visitRequestId},
     ${opts.visitInstanceId ?? 'NULL'}, NULL,
     'OPEN_VISIT_DETAIL', '${esc(url)}', NULL, FALSE, NOW())`);
  const id = scalar(`SELECT notification_id FROM notifications
     WHERE recipient_user_id = ${recipientId} ORDER BY notification_id DESC LIMIT 1`);
  return Number(id);
}

/**
 * Seeds a real-shaped VISIT_PRIVACY_CONSENT_WITHDRAWN row that (unlike the real producer) omits
 * visit_instance_id/campus_id even though the eventKey is campus-specific -- the MC-02 edge case
 * ("campus-specific semantic but the notification is missing the instance id"). No live producer
 * takes this path today (SubmitVisitSafeEditCommandHandler always resolves an exact instance when
 * exactly one campus is touched, and deliberately omits it when several are -- see its own §17
 * audit), so this fixture is seeded directly with the same metadataJson shape
 * NotificationEventKeys.BuildMetadata produces, to exercise the "no exact campus -> safe
 * request-level landing, never guess" branch on its own.
 */
export function insertCampusEventMissingInstance(opts: {
  recipientEmail: string; visitRequestId: number; requestCode: string; title: string; message: string;
}): number {
  const recipientId = userIdOf(opts.recipientEmail);
  // `params.requestCode` matches EXACTLY what the real producer supplies for this eventKey (see
  // SubmitVisitSafeEditCommandHandler) -- the frontend renders title/message from the i18n template +
  // these params whenever metadataJson names a recognized eventKey, ignoring the row's own raw
  // title/message columns entirely (those are legacy-only display fallbacks). A fixture with empty
  // params would render blank/placeholder text no click locator could ever match.
  const metadata = JSON.stringify({ eventKey: 'VISIT_PRIVACY_CONSENT_WITHDRAWN', params: { requestCode: opts.requestCode } });
  execDb(`INSERT INTO notifications
    (recipient_user_id, title, message, notification_type, category, priority, is_action_required,
     related_type, related_id, visit_request_id, visit_instance_id, campus_id,
     action_type, action_url, metadata_json, is_read, created_at)
    VALUES (${recipientId}, '${esc(opts.title)}', '${esc(opts.message)}',
     'VISIT_PRIVACY_CONSENT_WITHDRAWN', 'VISIT', 'URGENT', FALSE,
     'VisitRequest', ${opts.visitRequestId}, ${opts.visitRequestId}, NULL, NULL,
     'OPEN_VISIT_DETAIL', '/dashboard/visit?visitRequestId=${opts.visitRequestId}',
     '${esc(metadata)}', FALSE, NOW())`);
  const id = scalar(`SELECT notification_id FROM notifications
     WHERE recipient_user_id = ${recipientId} ORDER BY notification_id DESC LIMIT 1`);
  return Number(id);
}

/** Real backend action (Host invites the GENERAL department's active Leader) -- PARTICIPATION_INVITED. */
export async function inviteDeptSupport(
  request: APIRequestContext, hostKey: string, visitInstanceId: number, departmentId: number,
): Promise<number> {
  const res = await request.post(`${API_BASE}/delegations/visit-instances/${visitInstanceId}/participants/invite`, {
    headers: hdr(hostKey),
    data: { participantType: 'DEPT_SUPPORT', departmentId, message: 'Nho phong ho tro don tiep khach.' },
  });
  expect(res.ok(), `invite dept support failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  const body = await res.json();
  return body.participantId as number;
}

/** Real backend action: participant declines their invitation (PT-02 staleness precondition). */
export async function declineParticipation(request: APIRequestContext, participantKey: string, participantId: number) {
  const res = await request.post(`${API_BASE}/delegations/participants/${participantId}/respond`, {
    headers: hdr(participantKey), data: { accept: false, declineReason: 'Khong sap xep duoc thoi gian (E2E)' },
  });
  expect(res.ok(), `decline failed: ${res.status()} ${await res.text()}`).toBeTruthy();
}

/** Real backend action: the current Host hands the campus off to a different eligible user. */
export async function transferHost(
  request: APIRequestContext, currentHostKey: string, visitInstanceId: number,
  newHostUserId: number, expectedRowVersion: number,
) {
  const res = await request.post(`${API_BASE}/v2/visit-instances/${visitInstanceId}/host-transfer`, {
    headers: hdr(currentHostKey),
    data: { newHostUserId, reason: 'Ban giao E2E', expectedRowVersion },
  });
  expect(res.ok(), `host transfer failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  return res.json();
}

/** Any OTHER active STAFF user on the given campus, distinct from `excludeUserId` -- a real transfer target. */
export function anotherStaffOnCampus(campusId: number, excludeUserId: number): number | null {
  const id = scalar(
    `SELECT u.user_id FROM users u JOIN roles r ON r.role_id = u.role_id
     WHERE r.role_code = 'STAFF' AND u.status = 'ACTIVE' AND u.primary_campus_id = ${campusId}
       AND u.user_id != ${excludeUserId} ORDER BY u.user_id LIMIT 1`);
  return id ? Number(id) : null;
}

/** departmentId for a seeded GENERAL department's own Leader profile (dept_leader_hn / dept_staff_hn). */
export async function meDepartmentId(request: APIRequestContext, profileKey: string): Promise<number> {
  const res = await request.get(`${API_BASE}/auth/me`, { headers: hdr(profileKey) });
  const body = await res.json();
  return Number(body.user.departmentId);
}

/**
 * The most recent notification for a recipient, read through the REAL authenticated API right after
 * triggering the action that produced it -- never guessed from the producer's own source text. Used
 * to build an exact, non-fragile button locator in the browser (the button renders `title` + a
 * resolved message, so a substring of either always matches).
 */
export async function latestNotification(request: APIRequestContext, recipientKey: string) {
  const res = await request.get(`${API_BASE}/notifications?page=1&pageSize=5`, { headers: hdr(recipientKey) });
  expect(res.ok(), `list notifications failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  const body = await res.json();
  const items = body.items as Array<Record<string, unknown>>;
  expect(items.length, `no notifications at all for ${recipientKey}`).toBeGreaterThan(0);
  return items[0] as {
    notificationId: number; title: string; message: string | null; visitRequestId: number | null;
    visitInstanceId: number | null; actionType: string | null;
  };
}
