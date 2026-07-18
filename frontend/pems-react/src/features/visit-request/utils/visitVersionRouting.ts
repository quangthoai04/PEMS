import { v2DetailPath } from './formVersionErrors';

// ──────────────────────────────────────────────────────────────────────────────
// Version-aware routing for a visit request. The frontend decides v1 vs v2 UI from
// the request's `formSchemaVersion` (from the database) — NOT from the mixed flag,
// the campus count, or by waiting for a v1 endpoint to reply 409. This applies to
// BOTH v2 mixed and v2 non-mixed requests.
// ──────────────────────────────────────────────────────────────────────────────

/** form_schema_version >= 2 → per-campus v2. */
export const PER_CAMPUS_V2_MIN = 2;

/** A missing version (older cached payloads) is treated as legacy v1 — fail-safe to the v1 UI. */
export const isPerCampusV2 = (formSchemaVersion: number | null | undefined): boolean =>
  (formSchemaVersion ?? 1) >= PER_CAMPUS_V2_MIN;

export const v2EditPath = (visitRequestId: number | string): string =>
  `/dashboard/visit/v2/${visitRequestId}/edit`;

export const v2ResubmitPath = (visitRequestId: number | string): string =>
  `/dashboard/visit/v2/${visitRequestId}/resubmit`;

export const v1EditPath = (visitRequestId: number | string): string =>
  `/dashboard/visit/edit/${visitRequestId}`;

export const v1ResubmitPath = (visitRequestId: number | string): string =>
  `/dashboard/visit/resubmit/${visitRequestId}`;

export interface VisitRowRoutes {
  isV2: boolean;
  edit: string;
  resubmit: string;
  /** v2 detail is its own route; v1 detail uses the shared flat modal (null → open the modal). */
  detailRoute: string | null;
}

/**
 * Resolve the detail/edit/resubmit targets for a management-list row from its form schema version.
 * v2 (mixed or non-mixed) → v2 routes; v1 → the legacy routes/modal.
 */
export function resolveVisitRowRoutes(
  visitRequestId: number | string,
  formSchemaVersion: number | null | undefined,
): VisitRowRoutes {
  const v2 = isPerCampusV2(formSchemaVersion);
  return {
    isV2: v2,
    edit: v2 ? v2EditPath(visitRequestId) : v1EditPath(visitRequestId),
    resubmit: v2 ? v2ResubmitPath(visitRequestId) : v1ResubmitPath(visitRequestId),
    detailRoute: v2 ? v2DetailPath(visitRequestId) : null,
  };
}
