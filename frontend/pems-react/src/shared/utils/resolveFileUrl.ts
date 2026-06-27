/**
 * Resolves a file URL returned by the backend (e.g. `users.avatar_url` = `/api/files/123/content`)
 * into something usable in markup. Absolute URLs (Google Drive web-view links, etc.) are returned
 * untouched; relative `/api/...` paths are prefixed with the configured API origin so production only
 * has to change `VITE_API_BASE_URL`.
 *
 * `VITE_API_BASE_URL` already includes the `/api` segment in this project (e.g.
 * `http://localhost:5265/api`), and backend file URLs start with `/api`. To avoid an `/api/api`
 * double-prefix we strip a trailing `/api` from the base before joining.
 *
 * NOTE: private files (avatars) require an Authorization header, so a plain `<img src>` won't work —
 * use {@link useAuthenticatedImage} for those. This helper is for public assets and for building
 * absolute links where auth is not needed.
 */
export function resolveFileUrl(url?: string | null): string | null {
  if (!url) return null;

  if (url.startsWith('http://') || url.startsWith('https://')) {
    return url;
  }

  const rawBase = import.meta.env.VITE_API_BASE_URL ?? '';
  // Drop a trailing "/api" (and any trailing slash) so it doesn't double up with the "/api" path.
  const base = rawBase.replace(/\/+$/, '').replace(/\/api$/i, '');
  const path = url.startsWith('/') ? url : `/${url}`;

  return `${base}${path}`;
}
