import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';
import { loadVisitRequestDraft } from './visitRequestDraftStorage';
import { migrateV1DraftToV2 } from './visitRequestV2Form';

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
 */

const V2_DRAFT_KEY = 'pems_visit_registration_draft_percampus';
export const V2_DRAFT_SCHEMA_VERSION = 3;

const draftKey = (namespace?: string): string =>
  namespace ? `${V2_DRAFT_KEY}::${namespace}` : V2_DRAFT_KEY;

export type VisitRequestV2Draft = {
  draftSchemaVersion: typeof V2_DRAFT_SCHEMA_VERSION;
  savedAt: number;
  expiresAt: number;
  data: Partial<VisitRequestV2Schema>;
};

export type SaveV2DraftResult =
  | { success: true; savedAt: number; expiresAt: number }
  | { success: false; error: string };

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
  expiresInMs: number = 30 * 60 * 1000,
  namespace?: string,
): SaveV2DraftResult {
  if (!hasMeaningfulV2Data(data)) {
    return { success: false, error: 'No meaningful data to save' };
  }
  try {
    const payload: VisitRequestV2Draft = {
      draftSchemaVersion: V2_DRAFT_SCHEMA_VERSION,
      savedAt: Date.now(),
      expiresAt: Date.now() + expiresInMs,
      data: sanitizeV2Draft(data),
    };
    localStorage.setItem(draftKey(namespace), JSON.stringify(payload));
    return { success: true, savedAt: payload.savedAt, expiresAt: payload.expiresAt };
  } catch (error) {
    console.warn('Failed to save per-campus visit request draft', error);
    return { success: false, error: error instanceof Error ? error.message : 'Unknown storage error' };
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

/**
 * Loads the per-campus draft, falling back to a one-time in-memory migration of the
 * global single-form draft. The migrated result is NOT persisted here — it only becomes
 * the stored v3 draft once the user actually edits the form (autosave), so a newer v3
 * draft can never be clobbered by an older global one.
 */
export function loadVisitRequestV2DraftWithMigration(namespace?: string): V2DraftLoadResult {
  const own = loadVisitRequestV2Draft(namespace);
  if (own) return { draft: own, migratedFromGlobalDraft: false };

  const legacy = loadVisitRequestDraft(namespace);
  if (!legacy) return { draft: null, migratedFromGlobalDraft: false };

  return {
    draft: {
      draftSchemaVersion: V2_DRAFT_SCHEMA_VERSION,
      savedAt: legacy.savedAt,
      expiresAt: legacy.expiresAt,
      data: migrateV1DraftToV2(legacy.data),
    },
    migratedFromGlobalDraft: true,
  };
}

export function clearVisitRequestV2Draft(namespace?: string): void {
  try {
    localStorage.removeItem(draftKey(namespace));
  } catch (error) {
    console.warn('Failed to clear per-campus visit request draft', error);
  }
}
