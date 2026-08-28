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

/**
 * The same detail screen, opened for ONE campus of the request.
 *
 * A campus row in the list is a question about that campus, so the screen it opens answers about
 * that campus: section ② carries only it. Without this the row landed on the whole request and the
 * reader of TP.HCM was handed Hà Nội as well.
 *
 * A query parameter rather than a route segment: the target IS the request detail — same route,
 * same permissions, same data call — narrowed for reading. Dropping the parameter from the URL
 * gives back the full request, which is exactly the relationship between the two views.
 */
export const v2CampusDetailPath = (
  visitRequestId: number | string,
  visitInstanceId: number | string,
): string => `${v2DetailPath(visitRequestId)}?campus=${encodeURIComponent(String(visitInstanceId))}`;

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
