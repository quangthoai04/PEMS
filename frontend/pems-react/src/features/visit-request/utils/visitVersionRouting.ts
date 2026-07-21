import { v2DetailPath } from './formVersionErrors';

// ──────────────────────────────────────────────────────────────────────────────
// Version-aware routing for a visit request. The frontend decides v1 vs v2 UI from
// the request's `formSchemaVersion` (from the database) — NOT from the mixed flag,
// the campus count, or by waiting for a v1 endpoint to reply 409. This applies to
// BOTH v2 mixed and v2 non-mixed requests.
// ──────────────────────────────────────────────────────────────────────────────

/** form_schema_version >= 2 → per-campus v2. */
export const PER_CAMPUS_V2_MIN = 2;

/** In V2-only runtime, missing or < 2 is no longer treated as legacy v1, it's just invalid/retired. */
export const isPerCampusV2 = (formSchemaVersion: number | null | undefined): boolean =>
  (formSchemaVersion ?? 0) >= PER_CAMPUS_V2_MIN;

export const v2EditPath = (visitRequestId: number | string): string =>
  `/dashboard/visit/v2/${visitRequestId}/edit`;

export const v2ResubmitPath = (visitRequestId: number | string): string =>
  `/dashboard/visit/v2/${visitRequestId}/resubmit`;

export interface VisitRowRoutes {
  isV2: boolean;
  edit: string;
  resubmit: string;
  /** v2 detail is its own route; v1 detail uses the shared flat modal (null → open the modal). */
  detailRoute: string | null;
}

/**
 * Resolve the detail/edit/resubmit targets for a management-list row from its form schema version.
 * v2 (mixed or non-mixed) → v2 routes. Legacy/missing → unsupported error path.
 */
export function resolveVisitRowRoutes(
  visitRequestId: number | string,
  formSchemaVersion: number | null | undefined,
): VisitRowRoutes {
  const v2 = isPerCampusV2(formSchemaVersion);
  if (!v2) {
    return {
      isV2: false,
      edit: '/dashboard/visit/unsupported-version',
      resubmit: '/dashboard/visit/unsupported-version',
      detailRoute: '/dashboard/visit/unsupported-version',
    };
  }
  return {
    isV2: true,
    edit: v2EditPath(visitRequestId),
    resubmit: v2ResubmitPath(visitRequestId),
    detailRoute: v2DetailPath(visitRequestId),
  };
}
