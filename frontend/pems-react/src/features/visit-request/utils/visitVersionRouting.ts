import { v2DetailPath } from './formVersionErrors';

// ──────────────────────────────────────────────────────────────────────────────
// Routing for a visit request. In the Pure V2 runtime there is exactly one form
// version — per-campus v2 — so every request routes to the v2 detail/edit/resubmit
// screens. The backend no longer carries or emits form_schema_version, so nothing
// here branches on it: reading a field the server never sends had sent every row to
// a retired "unsupported-version" page that is not even routed.
// ──────────────────────────────────────────────────────────────────────────────

export const v2EditPath = (visitRequestId: number | string): string =>
  `/dashboard/visit/v2/${visitRequestId}/edit`;

export const v2ResubmitPath = (visitRequestId: number | string): string =>
  `/dashboard/visit/v2/${visitRequestId}/resubmit`;

export interface VisitRowRoutes {
  edit: string;
  resubmit: string;
  /** The per-campus v2 detail route. Always present — the flat v1 modal has no runtime left. */
  detailRoute: string;
}

/** Resolve the detail/edit/resubmit targets for a management-list row. Always the v2 screens. */
export function resolveVisitRowRoutes(visitRequestId: number | string): VisitRowRoutes {
  return {
    edit: v2EditPath(visitRequestId),
    resubmit: v2ResubmitPath(visitRequestId),
    detailRoute: v2DetailPath(visitRequestId),
  };
}
