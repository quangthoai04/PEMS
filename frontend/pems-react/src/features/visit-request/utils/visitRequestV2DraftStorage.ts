import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';

/**
 * Draft storage for the per-campus form v2.
 *
 * Draft schema versions across the feature's history:
 *   1 — legacy sessionStorage draft (long gone; still cleared defensively)
 *   2 — the GLOBAL single-form draft (`visitRequestDraftStorage.ts`, key
 *       `pems_public_visit_registration_draft`) — still owned by the v1 form
 *   3 — THIS per-campus shape (registrant + contact + campusVisits[] with stable
 *       clientKeys), stored under its OWN key so the v1 form and its draft are untouched.
 *
 * Reading falls back v3 → v2-migration (duplicate the global snapshot into every selected
 * campus). A stored v3 draft is NEVER overwritten by a migrated v2 draft: migration only
 * happens when no v3 draft exists.
 *
 * The draft also carries the SUBMISSION INTENT — the submissionId and, once an OTP has been
 * requested, which mailbox it went to and when it expires. Both are needed to survive a reload
 * without turning a retry into a second request: the backend treats the submissionId as the
 * idempotency key, so minting a new one per attempt is what creates duplicates.
 *
 * What is NOT here: the OTP code (never stored anywhere) and the challenge session token, which
 * lives in sessionStorage instead — see `readOtpChallengeToken`. A verification token in
 * localStorage would outlive the tab and the visit; the rest of the draft is the user's own typing.
 */

const V2_DRAFT_KEY = 'pems_visit_registration_draft_percampus';
const V2_OTP_TOKEN_KEY = 'pems_visit_registration_otp_challenge';
export const V2_DRAFT_SCHEMA_VERSION = 3;

const draftKey = (namespace?: string): string =>
  namespace ? `${V2_DRAFT_KEY}::${namespace}` : V2_DRAFT_KEY;

const otpTokenKey = (namespace?: string): string =>
  namespace ? `${V2_OTP_TOKEN_KEY}::${namespace}` : V2_OTP_TOKEN_KEY;

/**
 * The pending OTP challenge as the CLIENT needs to describe it: which mailbox was asked to prove
 * itself, how it is shown, and until when. No code, no hash, no token.
 */
export interface VisitRequestV2OtpContext {
  /** The registrant email the challenge was minted for — a different email invalidates it. */
  targetEmail: string;
  /** Server-rendered masked form, for display only. */
  maskedEmail: string;
  /** Wall-clock ISO string from the server, when it supplied one. */
  expiresAt?: string | null;
  resendAfterSeconds?: number | null;
  savedAt: number;
}

export type VisitRequestV2Draft = {
  draftSchemaVersion: typeof V2_DRAFT_SCHEMA_VERSION;
  savedAt: number;
  expiresAt: number;
  data: Partial<VisitRequestV2Schema>;
  /** Idempotency key for this submit intent — stable across OTP retries, resends and reloads. */
  submissionId?: string;
  otp?: VisitRequestV2OtpContext;
};

export type SaveV2DraftResult =
  | { success: true; savedAt: number; expiresAt: number }
  | { success: false; error: string };

export interface SaveV2DraftOptions {
  expiresInMs?: number;
  namespace?: string;
  submissionId?: string | null;
  /** `null` clears any stored context; `undefined` leaves the stored one untouched. */
  otp?: VisitRequestV2OtpContext | null;
}

export function hasMeaningfulV2Data(values: Partial<VisitRequestV2Schema> | null | undefined): boolean {
  if (!values) return false;
  const reg = values.registerInfo;
  const cp = values.contactPoint;
  return Boolean(
    reg?.fullName?.trim() ||
    reg?.organization?.trim() ||
    reg?.email?.trim() ||
    reg?.phone?.trim() ||
    cp?.fullName?.trim() || cp?.email?.trim() || cp?.phone?.trim() ||
    (values.partnerId !== undefined && values.partnerId !== null) ||
    values.campusVisits?.some(cv =>
      cv.campus?.trim() ||
      cv.startDatetime ||
      cv.endDatetime ||
      cv.delegationName?.trim() ||
      cv.purpose?.trim() ||
      cv.visitors?.some(v => v.fullName?.trim()) ||
      cv.supportTeam?.some(s => s.fullName?.trim()),
    ),
  );
}

/** Never persist OTP/session material or binary uploads inside a draft. */
export function sanitizeV2Draft(data: Partial<VisitRequestV2Schema>): Partial<VisitRequestV2Schema> {
  const cloned = JSON.parse(JSON.stringify(data)) as Partial<VisitRequestV2Schema> & Record<string, unknown>;
  delete cloned.otpCode;
  delete cloned.sessionToken;
  delete cloned.maskedEmail;
  delete cloned.uploadedFile;
  delete cloned.uploadedFiles;
  delete cloned.excelFile;
  return cloned;
}

export function saveVisitRequestV2Draft(
  data: Partial<VisitRequestV2Schema>,
  expiresInMs?: number,
  namespace?: string,
  options?: Omit<SaveV2DraftOptions, 'expiresInMs' | 'namespace'>,
): SaveV2DraftResult {
  if (!hasMeaningfulV2Data(data)) {
    return { success: false, error: 'No meaningful data to save' };
  }
  const ttl = expiresInMs ?? 30 * 60 * 1000;
  try {
    // Carry forward the submission intent unless this call states a new one: an autosave triggered
    // by a keystroke must not drop the submissionId an in-flight OTP challenge is bound to.
    const existing = readRawDraft(namespace);
    const payload: VisitRequestV2Draft = {
      draftSchemaVersion: V2_DRAFT_SCHEMA_VERSION,
      savedAt: Date.now(),
      expiresAt: Date.now() + ttl,
      data: sanitizeV2Draft(data),
      submissionId: options?.submissionId === null
        ? undefined
        : options?.submissionId ?? existing?.submissionId,
      otp: options?.otp === null ? undefined : options?.otp ?? existing?.otp,
    };
    localStorage.setItem(draftKey(namespace), JSON.stringify(payload));
    return { success: true, savedAt: payload.savedAt, expiresAt: payload.expiresAt };
  } catch (error) {
    console.warn('Failed to save per-campus visit request draft', error);
    return { success: false, error: error instanceof Error ? error.message : 'Unknown storage error' };
  }
}

/** Parses the stored draft without applying the TTL/version rules — internal use only. */
function readRawDraft(namespace?: string): VisitRequestV2Draft | null {
  try {
    const raw = localStorage.getItem(draftKey(namespace));
    return raw ? (JSON.parse(raw) as VisitRequestV2Draft) : null;
  } catch {
    return null;
  }
}

export function loadVisitRequestV2Draft(namespace?: string): VisitRequestV2Draft | null {
  try {
    const raw = localStorage.getItem(draftKey(namespace));
    if (!raw) return null;
    const parsed = JSON.parse(raw) as VisitRequestV2Draft;
    if (
      !parsed?.data ||
      !parsed?.expiresAt ||
      Date.now() > parsed.expiresAt ||
      parsed.draftSchemaVersion !== V2_DRAFT_SCHEMA_VERSION
    ) {
      localStorage.removeItem(draftKey(namespace));
      clearOtpChallengeToken(namespace);
      return null;
    }
    return parsed;
  } catch (error) {
    console.warn('Failed to load per-campus visit request draft', error);
    try {
      localStorage.removeItem(draftKey(namespace));
    } catch { /* storage unavailable */ }
    return null;
  }
}

export interface V2DraftLoadResult {
  draft: VisitRequestV2Draft | null;
  /** True when the returned data was migrated on the fly from the GLOBAL (v1-form) draft. */
  migratedFromGlobalDraft: boolean;
}

export function loadVisitRequestV2DraftWithMigration(namespace?: string): V2DraftLoadResult {
  const own = loadVisitRequestV2Draft(namespace);
  return { draft: own, migratedFromGlobalDraft: false };
}

export function clearVisitRequestV2Draft(namespace?: string): void {
  try {
    localStorage.removeItem(draftKey(namespace));
  } catch (error) {
    console.warn('Failed to clear per-campus visit request draft', error);
  }
  clearOtpChallengeToken(namespace);
}

// ── OTP challenge token (sessionStorage) ─────────────────────────────────────
// The token is what lets the user finish a challenge they already asked for after a reload, so it
// has to outlive the React tree. It does NOT belong in localStorage: that is shared across every
// tab and survives the browser being closed, while this is only meaningful for as long as the tab
// the challenge was started in. It is stored WITH its submissionId, so a token can never be
// replayed against a different submit intent.

interface StoredOtpChallenge {
  submissionId: string;
  sessionToken: string;
}

export function saveOtpChallengeToken(
  submissionId: string, sessionToken: string, namespace?: string,
): void {
  try {
    sessionStorage.setItem(
      otpTokenKey(namespace), JSON.stringify({ submissionId, sessionToken } satisfies StoredOtpChallenge));
  } catch (error) {
    // A blocked sessionStorage costs the resume affordance, never the draft.
    console.warn('Failed to store the OTP challenge token', error);
  }
}

/** The stored token for THIS submission intent, or null when there is none that matches. */
export function readOtpChallengeToken(submissionId: string, namespace?: string): string | null {
  try {
    const raw = sessionStorage.getItem(otpTokenKey(namespace));
    if (!raw) return null;
    const parsed = JSON.parse(raw) as StoredOtpChallenge;
    return parsed?.submissionId === submissionId ? parsed.sessionToken ?? null : null;
  } catch {
    return null;
  }
}

export function clearOtpChallengeToken(namespace?: string): void {
  try {
    sessionStorage.removeItem(otpTokenKey(namespace));
  } catch { /* storage unavailable */ }
}

/**
 * Draft namespace for a signed-in user. One key per ACCOUNT (never per email address, which is PII
 * in a storage key and changes shape between surfaces): the modal on the dashboard and the
 * standalone create route are the same person's draft and must not fork into two.
 * Public visitors get `undefined` — a separate key that authenticated drafts never touch.
 */
export const visitDraftNamespace = (userId: number | string | null | undefined): string | undefined =>
  userId === null || userId === undefined || userId === '' ? undefined : `u${userId}`;
