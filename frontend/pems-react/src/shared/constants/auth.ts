/**
 * Frontend authentication configuration, driven by Vite env vars. Mirrors the
 * backend AuthOptions. In ProductionSsoOnly mode password login is force-disabled
 * regardless of VITE_ENABLE_PASSWORD_LOGIN.
 *
 *   VITE_AUTH_MODE=DevMixed | ProductionSsoOnly
 *   VITE_ENABLE_PASSWORD_LOGIN=true|false
 *   VITE_ENABLE_GOOGLE_SSO=true|false
 */
export type AuthMode = 'DevMixed' | 'ProductionSsoOnly';

const env = import.meta.env as unknown as Record<string, string | undefined>;

function flag(value: string | undefined, fallback: boolean): boolean {
  if (value === undefined || value === null || value === '') return fallback;
  // Never use Boolean(value): the string "false" would coerce to true.
  return value.toLowerCase() === 'true' || value === '1';
}

export const AUTH_MODE: AuthMode =
  env.VITE_AUTH_MODE === 'ProductionSsoOnly' ? 'ProductionSsoOnly' : 'DevMixed';

const isProductionSsoOnly = AUTH_MODE === 'ProductionSsoOnly';

export const AUTH_CONFIG = {
  mode: AUTH_MODE,
  isProductionSsoOnly,
  /** Password (email/password) login — never available in ProductionSsoOnly. */
  enablePasswordLogin: !isProductionSsoOnly && flag(env.VITE_ENABLE_PASSWORD_LOGIN, true),
  enableGoogleSso: flag(env.VITE_ENABLE_GOOGLE_SSO, true),
  /** Google OAuth client id; empty means SSO is enabled but not yet configured. */
  googleClientId: (env.VITE_GOOGLE_CLIENT_ID ?? '').trim(),
} as const;

if (import.meta.env.DEV) {
  console.log('[ENV]', {
    mode: import.meta.env.MODE,
    googleClientId: import.meta.env.VITE_GOOGLE_CLIENT_ID,
    authConfigGoogleClientId: AUTH_CONFIG.googleClientId,
    enableGoogleSso: AUTH_CONFIG.enableGoogleSso,
  });
}

