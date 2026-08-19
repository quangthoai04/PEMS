/**
 * Endpoints that are `[AllowAnonymous]` on the backend — their whole job is to accept NEW
 * credentials/tokens and issue a fresh session, so they must stay reachable no matter what OLD
 * session the browser happens to be holding in `localStorage`.
 *
 * Used by `authInterceptor` to never attach a stale Bearer token to these requests (defense in
 * depth — the backend's own `SessionValidationMiddleware`/`[AllowAnonymous]` already enforce the
 * real rule, but a client that never sends the stale header in the first place cannot depend on
 * that being right everywhere). `/auth/logout` is deliberately NOT here: it is `[Authorize]` and
 * needs the bearer to identify which session to revoke — see `httpClient.ts`'s `NO_REFRESH_PATHS`,
 * which is this list PLUS `/auth/logout` for the unrelated "don't run the refresh-retry loop"
 * concern that logout does share.
 */
export const PUBLIC_AUTH_PATHS = [
  '/auth/login',
  '/auth/google',
  '/auth/feid',
  '/auth/refresh',
  '/auth/forgot-password',
  '/auth/reset-password',
];
