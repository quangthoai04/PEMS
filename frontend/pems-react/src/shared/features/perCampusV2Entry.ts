// ──────────────────────────────────────────────────────────────────────────────
// Canonical per-campus v2 entry routes + the single branching decision every entry
// point uses. Centralised so the "v2 route vs v1 popup" choice — and the exact paths
// — live in ONE place, never scattered/guessed across components.
// ──────────────────────────────────────────────────────────────────────────────

/** Public (anonymous, OTP) per-campus v2 registration page. */
export const V2_PUBLIC_REGISTRATION_PATH = '/visit-registration/v2';

/** Authenticated per-campus v2 create page. */
export const V2_AUTHENTICATED_CREATE_PATH = '/visit/create-v2';

/** Route to the v2 page, or fall back to opening the v1 form popup. */
export type VisitEntryDecision =
  | { kind: 'v2-route'; to: string }
  | { kind: 'v1-popup' };

/**
 * Public homepage CTA. When the v2 capability is enabled (both flags on), route to the
 * public v2 registration page; otherwise (OFF, still loading, or errored) open the v1 popup.
 */
export function resolvePublicVisitEntry(v2Enabled: boolean): VisitEntryDecision {
  return v2Enabled
    ? { kind: 'v2-route', to: V2_PUBLIC_REGISTRATION_PATH }
    : { kind: 'v1-popup' };
}

/**
 * Authenticated "create visit request" action. When v2 is enabled, route to the v2 create
 * page; otherwise open the v1 authenticated create popup.
 */
export function resolveAuthenticatedCreateEntry(v2Enabled: boolean): VisitEntryDecision {
  return v2Enabled
    ? { kind: 'v2-route', to: V2_AUTHENTICATED_CREATE_PATH }
    : { kind: 'v1-popup' };
}
