import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';
import { createEmptyCampusVisit } from './visitRequestV2Form';

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

/**
 * The one refusal that is not a fault: there was simply nothing worth writing. Callers tell it apart
 * from a storage failure so they can say the right thing instead of a blanket "could not save".
 */
export const V2_DRAFT_NOTHING_TO_SAVE = 'No meaningful data to save';

export interface SaveV2DraftOptions {
  expiresInMs?: number;
  namespace?: string;
  submissionId?: string | null;
  /** `null` clears any stored context; `undefined` leaves the stored one untouched. */
  otp?: VisitRequestV2OtpContext | null;
}

/**
 * The values a card is BORN with — and the request-level selection mode a form starts on. A field
 * still sitting on one of these is a field nobody has touched; anything else is a choice the user
 * made, and a choice is worth keeping.
 *
 * READ from `createEmptyCampusVisit` rather than repeated as literals. When they were two separate
 * copies of the same three values they drifted: the born card said one thing, this file said
 * another, and the field the two disagreed about (the media consent) then read as "untouched" no
 * matter what the user answered — so a form whose only edit was that answer was silently declared
 * empty and never written to disk.
 */
const BORN_CAMPUS_VISIT = createEmptyCampusVisit('untouched-sentinel');
const UNTOUCHED_VISIT_TYPE = BORN_CAMPUS_VISIT.visitType;
const UNTOUCHED_WORKING_LANGUAGE = BORN_CAMPUS_VISIT.workingLanguage;
const UNTOUCHED_MEDIA_CONSENT = BORN_CAMPUS_VISIT.mediaConsentStatus;
const UNTOUCHED_PARTNER_MODE = 'NEW_ORGANIZATION';

const filled = (value: unknown): boolean => typeof value === 'string' && value.trim().length > 0;

/** A visitor / support row counts the moment ANY of its four columns has something in it. */
const personRowHasContent = (person: unknown): boolean => {
  const p = person as Record<string, unknown> | null | undefined;
  return Boolean(p) && (
    filled(p?.fullName) || filled(p?.jobTitle) || filled(p?.organization) || filled(p?.nationality)
  );
};

/**
 * The request-level "Yêu cầu bổ sung" (workingLanguage/mediaConsentStatus/transportationNote/notes,
 * plan: request-level additional requirements). Shares the exact born-value comparison
 * `campusVisitHasContent` uses for the same 2 enums — the two fields used to live on the campus
 * card and moved here, so "untouched" must still mean the same thing it always did.
 */
const additionalRequirementsHasContent = (value: unknown): boolean => {
  const r = value as Record<string, unknown> | null | undefined;
  return Boolean(r) && (
    filled(r?.transportationNote) || filled(r?.notes)
    || (filled(r?.workingLanguage) && r?.workingLanguage !== UNTOUCHED_WORKING_LANGUAGE)
    || (filled(r?.mediaConsentStatus) && r?.mediaConsentStatus !== UNTOUCHED_MEDIA_CONSENT)
  );
};

const operationalContactHasContent = (contact: unknown): boolean => {
  const c = contact as Record<string, unknown> | null | undefined;
  return Boolean(c) && (
    filled(c?.fullName) || filled(c?.organization) || filled(c?.jobTitle)
    || filled(c?.phone) || filled(c?.email)
  );
};

const campusVisitHasContent = (visit: unknown): boolean => {
  const cv = visit as Record<string, unknown> | null | undefined;
  if (!cv) return false;
  // `clientKey`, `visitInstanceId` and `expectedRowVersion` are deliberately absent: they are
  // bookkeeping the form mints for itself, and a draft built out of them would be an empty form.
  return Boolean(
    filled(cv.campus) || filled(cv.startDatetime) || filled(cv.endDatetime)
    || filled(cv.delegationName) || filled(cv.purpose) || filled(cv.workingContent)
    || filled(cv.visitTypeOther) || filled(cv.transportationNote)
    || filled(cv.notes)
    || (filled(cv.visitType) && cv.visitType !== UNTOUCHED_VISIT_TYPE)
    || (filled(cv.workingLanguage) && cv.workingLanguage !== UNTOUCHED_WORKING_LANGUAGE)
    || (filled(cv.mediaConsentStatus) && cv.mediaConsentStatus !== UNTOUCHED_MEDIA_CONSENT)
    || operationalContactHasContent(cv.operationalContact)
    // Choosing MEMBER or EXTERNAL is itself a decision worth keeping, even before anything else on
    // the card is filled in — losing it because "nothing counts yet" would mean answering the
    // source question and then closing the modal silently discards that answer (plan
    // CanhIter3FixBug §14/§26).
    || cv.operationalContactSource === 'MEMBER' || cv.operationalContactSource === 'EXTERNAL'
    || (Array.isArray(cv.visitors) && cv.visitors.some(personRowHasContent))
    || (Array.isArray(cv.supportTeam) && cv.supportTeam.some(personRowHasContent)),
  );
};

/**
 * "Is there anything here worth writing to disk?" — asked before every save, and NOT the same
 * question as "has the user changed the form" (that one is `formState.isDirty`, which is what the
 * close prompt is driven by).
 *
 * It covers EVERY field the schema lets a user fill, because the ones it missed were silently
 * unsaveable: a form carrying only a job title, a nationality, the working content, an operational
 * contact, or a visitor row without a name was answered with "No meaningful data to save" and the
 * typing was lost on close. Anything a fresh form already contains — the client keys, the single
 * empty campus card, the empty visitor row, the default enums — still counts as nothing.
 */
export function hasMeaningfulV2Data(values: Partial<VisitRequestV2Schema> | null | undefined): boolean {
  if (!values) return false;
  const reg = values.registerInfo as Record<string, unknown> | undefined;
  return Boolean(
    filled(reg?.fullName) || filled(reg?.organization) || filled(reg?.jobTitle)
    || filled(reg?.phone) || filled(reg?.email) || filled(reg?.nationality)
    || (values.partnerId !== undefined && values.partnerId !== null)
    || (filled(values.partnerSelectionMode) && values.partnerSelectionMode !== UNTOUCHED_PARTNER_MODE)
    || additionalRequirementsHasContent(values.additionalRequirements)
    || (Array.isArray(values.campusVisits) && values.campusVisits.some(campusVisitHasContent)),
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
    return { success: false, error: V2_DRAFT_NOTHING_TO_SAVE };
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
